# sheep dog n wolf (sheep raider) reverse engineering stuff

everything is based off of the PAL PC version and may or may not work for PS1\
also this stuff is very WIP :)

## structure:
`exporter`: go CLI tool for exporting models/sounds/textures/etc.\
`imhex patterns`: file patterns written for imhex for various game formats\
`headers from disc`: header/dev files left by infogrames in the PAL PC disc, specifically in `Levels/Lvl-03`\
`war_crc32_writer.go`: go script for writing new checksums to modified .WAR files