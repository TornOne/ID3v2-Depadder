# ID3v2-Depadder
ID3v2 Depadder removes padding and uncompression from ID3v2 tags, which are popular for tagging MP3 and other MPEG audio files.  
No information will be lost, and the file size is usually reduced by between 2KB and 1% of the original file size.  
It was made for and tested mostly on ID3v2.4 tags, but should also work for ID3v2.3 tags. Any older versions have not been tested.

I use it to reduce the size of my music collection after tagging it. For example, with [Mp3tag](https://www.mp3tag.de/en/), it can be added under Tools with a parameter of `"%_path%"`, and then be used on multiple files at once.  
If you're looking to reduce the size of your FLAC files, I can recommend [metaflac](https://xiph.org/flac/documentation_tools_metaflac.html) `--dont-use-padding --remove --block-type=PADDING "%_path%"`

## Usage
	"ID3v2 Depadder.exe" filename [-s]
`-s` enables silent mode (disabled by default). This will suppress errors and warnings, succeeding or failing silently, and will take default actions instead of prompting the user.

### Disclaimer
This program alters the files it is run on. It has been tested, but there is a nonzero chance it could break your files.  
If you want to be safe, back up your files before using this program.  I am not responsible for any damages.
