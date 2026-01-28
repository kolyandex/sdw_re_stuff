# exporter

sorry in advance for the utterly stupid code in here, lmao\
build with `go build` and run `./sdw` to get a list of commands\
then run `./sdw <subcommand> --help` for a list of flags\
\
if you're exporting models from the PS1 version, omit the .DAV filepath to export models without textures.\
those are formatted differently from the PC version and will make the exporter crash and burn

## basic usage
### export a level with actors and trigger boxes
`sdw lvlexport -p level.war -d level.dav -o level.glb`

### export all the models from a level (not including level geometry)
`sdw modelfolder -p level.war -d level.dav -o models`

### export the skybox model
`sdw skyboxexport -p level.war -d level.dav -o skybox.glb`

### extract textures
`sdw dav -p level.dav -o textures`\
`sdw davpages -p level.dav -o atlases` (for texture atlases)

### extract sound effects
`sdw snd -p level.snd -o sounds`

### extract strings from a .BSM
`sdw bsm -p GREET_txt.BSM -o strings.json`