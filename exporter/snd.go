package main

import (
	"encoding/binary"
	"fmt"
	"io/fs"
	"os"
	"path/filepath"
	"strconv"
	"strings"
)

type SNDHeader struct {
	Unused    uint32
	Magic     [4]uint8
	NumSounds uint32
}

type SNDSound struct {
	ID     uint32
	Size   uint32
	Unused uint32
}

var sndIds = []uint32{
	4,
	40,
	72,
	81,
	87,
	120,
	196,
	199,
	202,
	236,
	237,
	280,
	73,
	5,
	14,
	77,
	109,
	78,
	82,
	83,
	29,
	80,
	52,
	54,
	62,
	90,
	129,
	134,
	176,
	186,
	187,
	188,
	215,
	216,
	251,
	268,
	18,
	102,
	110,
	296,
	68,
	264,
	262,
	311,
	91,
	246,
	61,
	322,
	323,
	324,
	274,
	235,
	284,
	93,
	57,
	55,
	98,
	99,
	94,
	248,
	250,
	249,
}

var sndFilenames = []string{
	"SCOATTPD",
	"SCOCOLD",
	"SCOSTEP1",
	"SCOCHOLE",
	"SCORECPT",
	"S07NWALK",
	"SCOSRUN1",
	"SCOSRUN4",
	"SCOFALL",
	"SCOGLISS",
	"SBUSAUT",
	"SRBELECT",
	"SCOSTARS",
	"SCOATTET",
	"SCOJUMP1",
	"SCOMVT2",
	"SCOJUMP2",
	"SCOMVT3",
	"SCOBRAK1",
	"SCOBRAK2",
	"SCOHITSM",
	"SCOCWALL",
	"SCOSTEAM",
	"SBONMBTN",
	"SMOJPMOV",
	"SGLCHGLI",
	"S04FEMOR",
	"S08CRIBA",
	"S14VENT",
	"S09PRAR1",
	"S09PRAR2",
	"S09PRAR3",
	"S16DESER",
	"S16RAPAC",
	"SGLPLANE",
	"S09PRAR4",
	"SCOLBOX",
	"SGLCBRAK",
	"SFUNTEUF",
	"SFUTKFRK",
	"SR2ROFA2",
	"SBSTIMOV",
	"SCOPUSH",
	"SGORUN",
	"SSAWALKN",
	"SSAJPLND",
	"SMOEATIN",
	"SMOBLONE",
	"SMOGROUP",
	"SMOGROU2",
	"SMOSLEEP",
	"SEAELAST",
	"SEABOUCL",
	"SR4EXPRO",
	"SGRRAIOP",
	"SPGNIV",
	"SDYMECHE",
	"SDYDYNAM",
	"SPFASCEN",
	"SPTAIGU",
	"SPTACTIO",
	"SPTSIFF",
}

var SNDMagic = [4]uint8{'v', 'd', 'x', '7'}

func sndExtract(path string, outPath string) {
	file, err := os.Open(path)
	check(err)

	sndHeader := SNDHeader{}
	check(binary.Read(file, binary.LittleEndian, &sndHeader))

	if sndHeader.Magic != SNDMagic {
		panic("invalid SND magic")
	}

	err = os.MkdirAll(outPath, 0755)
	check(err)

	for range sndHeader.NumSounds {
		sndSound := SNDSound{}
		check(binary.Read(file, binary.LittleEndian, &sndSound))

		wavData := make([]uint8, sndSound.Size)
		check(binary.Read(file, binary.LittleEndian, wavData))

		var filename string

		var origFilename string
		for i, id := range sndIds {
			if sndSound.ID == id {
				origFilename = sndFilenames[i]
				break
			}
		}
		if len(origFilename) != 0 {
			filename = fmt.Sprintf("%s.WAV", origFilename)
		} else {
			filename = fmt.Sprintf("%d.wav", sndSound.ID)
		}

		func() {
			outFile, err := os.Create(filepath.Join(outPath, filename))
			check(err)
			defer outFile.Close()

			check(binary.Write(outFile, binary.LittleEndian, wavData))
		}()
	}
}

/*func getSndIds(idsPath string) []uint32 {
	idsFile, err := os.Open(idsPath)
	check(err)
	defer idsFile.Close()

	sndHeader := SNDHeader{}
	check(binary.Read(idsFile, binary.LittleEndian, &sndHeader))

	if sndHeader.Magic != SNDMagic {
		panic("invalid SND magic")
	}

	sndIds := make([]uint32, sndHeader.NumSounds)

	for i := range sndHeader.NumSounds {
		sndSound := SNDSound{}
		check(binary.Read(idsFile, binary.LittleEndian, &sndSound))

		sndIds[i] = sndSound.ID

		idsFile.Seek(int64(sndSound.Size), 1)
	}

	return sndIds
}

func sndPack(idsPath string, wavPath string, outPath string) {
	sndIds := getSndIds(idsPath)

	wav, err := os.ReadFile(wavPath)
	check(err)

	outFile, err := os.Create(outPath)
	check(err)
	defer outFile.Close()

	header := SNDHeader{
		Unused:    0,
		Magic:     SNDMagic,
		NumSounds: uint32(len(sndIds)),
	}
	check(binary.Write(outFile, binary.LittleEndian, header))

	for i := range len(sndIds) {
		sound := SNDSound{
			ID:     sndIds[i],
			Size:   uint32(len(wav)),
			Unused: 0,
		}
		check(binary.Write(outFile, binary.LittleEndian, sound))
		check(binary.Write(outFile, binary.LittleEndian, wav))
	}
}*/

func sndPack(wavDirPath string, sndPath string) {
	sounds := make(map[uint32]string)

	check(filepath.WalkDir(wavDirPath, func(path string, d fs.DirEntry, err error) error {
		check(err)

		if d.IsDir() {
			return nil
		}

		filename := d.Name()
		filename = strings.TrimSuffix(filename, filepath.Ext(filename))

		id, err := strconv.Atoi(filename)
		sndId := uint32(id)
		idFound := false
		if err != nil {
			for i, n := range sndFilenames {
				if n == filename {
					sndId = sndIds[i]
					idFound = true
					break
				}
			}
		} else {
			idFound = true
		}

		if !idFound {
			fmt.Printf("ID for %s not found, skipping\n", filename)
			return nil
		}

		sounds[sndId] = path

		return nil
	}))

	sndFile, err := os.Create(sndPath)
	check(err)
	defer sndFile.Close()

	header := SNDHeader{
		Unused:    0,
		Magic:     SNDMagic,
		NumSounds: uint32(len(sounds)),
	}
	check(binary.Write(sndFile, binary.LittleEndian, header))

	for id, path := range sounds {
		wav, err := os.ReadFile(path)
		check(err)

		sound := SNDSound{
			ID:     id,
			Size:   uint32(len(wav)),
			Unused: 0,
		}
		check(binary.Write(sndFile, binary.LittleEndian, sound))
		check(binary.Write(sndFile, binary.LittleEndian, wav))
	}
}
