using System;
using System.Collections.Generic;
using System.IO;

class Depadder {
	static void Main(string[] args) {
		byte[] file = File.ReadAllBytes(args[0]);
		bool silent = args.Length > 1 && args[1] == "-s";

		//Parse ID3v2.x tag header
		int headerStart = Find(file, "ID3");
		if (headerStart == -1) {
			Exit(1, "ID3v2.x tag not found", silent);
		}
		byte majorVersion = file[headerStart + 3];
		byte minorVersion = file[headerStart + 4];
		if (majorVersion > 4) {
			Exit(2, $"Major version mismatch. 2.4.0 expected, 2.{majorVersion}.{minorVersion} found.", silent);
		}
		if (!silent && majorVersion == 4 && minorVersion != 0) {
			Console.WriteLine($"Minor version mismatch. 2.4.0 expected, 2.4.{minorVersion} found. Continuing execution.");
		}
		byte tagFlags = file[headerStart + 5];
		bool tagUnsynchronization = (tagFlags >> 7) == 1;
		bool extendedHeader = (tagFlags >> 6 & 1) == 1;
		bool footerPresent = (tagFlags >> 4 & 1) == 1; //Should theoretically mean no padding is present
		if (tagFlags << 28 != 0) {
			Exit(3, $"Invalid flags. xxxx0000 expected, {Convert.ToString(tagFlags, toBase: 2)} found.", silent);
		}
		int tagSize = ReadSynchsafeInt(file, headerStart + 6);
		int tagEnd = headerStart + 9 + tagSize;
		if (tagEnd >= file.Length) {
			Exit(4, $"Tag exceeds length of file. File has {file.Length} bytes, tag ends on byte {tagSize}.", silent);
		}
		//Header found, very likely to match ID3v2.4 tag, can continue

		//In case of a ID3v2.3 (or lower?) tag, first remove unsynchronization from the tags, then assume it doesn't exist
		if (majorVersion < 4 && tagUnsynchronization) {
			List<byte> newFile = new List<byte>();
			int blockStart = 0, blockEnd;
			for (int pos = headerStart + 10; pos < tagEnd; pos++) {
				if (file[pos] == 255 && file[pos + 1] == 0) {
					blockEnd = ++pos;
					newFile.AddRange(new ArraySegment<byte>(file, blockStart, blockEnd - blockStart));
					blockStart = pos + 1;
				}
			}
			newFile.AddRange(new ArraySegment<byte>(file, blockStart, file.Length - blockStart));
			file = newFile.ToArray();
		}

		file[headerStart + 5] = (byte)(tagFlags & 0b01111111); //Disable unsynchronization, whatever it was before
		int currentPos = headerStart + 10;
		List<(int start, int end)> blocksToRemove = new List<(int, int)>();

		//If the extended header is present, remove it. It's not necessary, and modifications we make may invalidate it.
		if (extendedHeader) {
			blocksToRemove.Add((currentPos, (currentPos += ReadInt(file, currentPos, majorVersion == 4)) - 1));
		}

		//Go through all frames until you reach a null byte or the end of the tag.
		while (currentPos <= tagEnd && file[currentPos] != 0) {
			string frameID = new string(new char[] { (char)file[currentPos], (char)file[currentPos + 1], (char)file[currentPos + 2], (char)file[currentPos + 3] });
			int frameSize = ReadInt(file, currentPos + 4, majorVersion == 4);
			int frameEnd = currentPos + frameSize + 9;
			byte flags1 = file[currentPos + 8];
			byte flags2 = file[currentPos + 9];
			bool discard, unsynchronised = false;
			if (majorVersion == 4) {
				discard = (flags1 >> 6 & 1) == 1;
				unsynchronised = (flags2 >> 1 & 1) == 1;
			} else {
				discard = (flags1 >> 7) == 1;
			}

			if (discard) { //Prompt for removal of any frames that want to be discarded if the tag is altered in any way
				if (!silent) {
					Console.WriteLine($"Frame {frameID} asks to be discarded, [y/n]?");
				}
				if (silent || Console.ReadKey().Key == ConsoleKey.Y) {
					blocksToRemove.Add((currentPos, frameEnd));
				}
			} else if (unsynchronised) { //Remove unsynchronization from frames which use them
				file[currentPos + 9] = (byte)(flags2 & 0b11111101); //Disable unsynchronization
				int unsynchronizationCount = 0;

				for (currentPos += 10; currentPos < frameEnd; currentPos++) {
					if (file[currentPos] == 255 && file[currentPos + 1] == 0) {
						blocksToRemove.Add((currentPos + 1, currentPos + 1));
						unsynchronizationCount++;
					}
				}

				WriteInt(file, frameEnd - frameSize - 5, frameSize - unsynchronizationCount, majorVersion == 4); //Update frame size
			}

			currentPos = frameEnd + 1;
		}

		//Everything from the encountered null byte to the end of the tag is padding to be removed
		if (currentPos <= tagEnd) {
			blocksToRemove.Add((currentPos, tagEnd));
			if (footerPresent && !silent) {
				Console.WriteLine("Footer and padding both present - invalid ID3v2 tag. Continuing execution.");
			}
		}

		if (blocksToRemove.Count > 0) {
			//Adjust the tagSize to fit the new size
			foreach ((int start, int end) in blocksToRemove) {
				tagSize -= end - start + 1;
			}
			WriteSynchsafeInt(file, headerStart + 6, tagSize);

			//Overwrite the original file, excluding listed blocks
			using (FileStream fileOut = File.Create(args[0])) {
				for (int i = 0; i <= blocksToRemove.Count; i++) {
					int start = i == 0 ? 0 : (blocksToRemove[i - 1].end + 1);
					int end = i < blocksToRemove.Count ? blocksToRemove[i].start : file.Length;
					if (end > start) {
						fileOut.Write(file, start, end - start);
					}
				}
			}
		}
	}

	static int Find(byte[] array, string wordString, int startPos = 0) {
		byte[] word = Array.ConvertAll(wordString.ToCharArray(), Convert.ToByte);

		for (int matchCounter = 0, i = startPos; i < array.Length; i++) {
			if (array[i] == word[matchCounter]) {
				if (++matchCounter == word.Length) {
					return i - matchCounter + 1;
				}
			} else {
				matchCounter = 0;
			}
		}

		return -1;
	}

	static int ReadSynchsafeInt(byte[] array, int pos) => array[pos] << 21 | array[pos + 1] << 14 | array[pos + 2] << 7 | array[pos + 3];

	static int ReadInt(byte[] array, int pos) => array[pos] << 24 | array[pos + 1] << 16 | array[pos + 2] << 8 | array[pos + 3];

	static int ReadInt(byte[] array, int pos, bool synchsafe) => synchsafe ? ReadSynchsafeInt(array, pos) : ReadInt(array, pos);

	static void WriteSynchsafeInt(byte[] array, int pos, int num) {
		array[pos] = (byte)(num >> 21 & 0b01111111);
		array[pos + 1] = (byte)(num >> 14 & 0b01111111);
		array[pos + 2] = (byte)(num >> 7 & 0b01111111);
		array[pos + 3] = (byte)(num & 0b01111111);
	}

	static void WriteInt(byte[] array, int pos, int num) {
		array[pos] = (byte)(num >> 24);
		array[pos + 1] = (byte)(num >> 16);
		array[pos + 2] = (byte)(num >> 8);
		array[pos + 3] = (byte)num;
	}

	static void WriteInt(byte[] array, int pos, int num, bool synchsafe) {
		if (synchsafe) {
			WriteSynchsafeInt(array, pos, num);
		} else {
			WriteInt(array, pos, num);
		}
	}

	static void Exit(int exitCode = 0, string message = "", bool silent = false) {
		if (!silent && message != "") {
			Console.WriteLine(message);
			Console.ReadKey(true);
		}
		Environment.Exit(exitCode);
	}
}
