package main

import (
	"encoding/binary"
	"encoding/json"
	"math"
	"os"

	"golang.org/x/text/encoding/charmap"
	"golang.org/x/text/transform"
)

type BSMLang uint8

const (
	English    BSMLang = 0x1
	French     BSMLang = 0x2
	German     BSMLang = 0x4
	Dutch      BSMLang = 0x8
	Spanish    BSMLang = 0x10
	Italian    BSMLang = 0x20
	Portuguese BSMLang = 0x40
)

type BSMString struct {
	header BSMStringHead
	data   []byte
}

type BSMStringHead struct {
	Language  BSMLang
	StringID  uint16
	SectionID uint8
	Size      uint32
}

type BSMHeader struct {
	Magic [12]byte
	Size  uint32
}

var langNames = map[BSMLang]string{
	English:    "english",
	French:     "french",
	German:     "german",
	Dutch:      "dutch",
	Spanish:    "spanish",
	Italian:    "italian",
	Portuguese: "portuguese",
}

var langIds = map[string]BSMLang{
	"english":    English,
	"french":     French,
	"german":     German,
	"dutch":      Dutch,
	"spanish":    Spanish,
	"italian":    Italian,
	"portuguese": Portuguese,
}

var BSMMagic = [12]byte{'G', 'R', 'E', 'E', 'T', 'I', 'N', 'G', 'V', '1', '.', '0'}

func bsmToJson(bsmPath string, jsonPath string) {
	bsmFile, err := os.Open(bsmPath)
	check(err)
	defer bsmFile.Close()

	header := BSMHeader{}
	check(binary.Read(bsmFile, binary.LittleEndian, &header))

	if header.Magic != BSMMagic {
		panic("Invalid BSM magic")
	}

	languages := make(map[string]map[uint8][]string, 7)
	for _, lang := range langNames {
		languages[lang] = make(map[uint8][]string)
		//for section := range 8 {
		//	languages[lang][uint8(section)] = make([]string)
		//}
	}

	for range header.Size {
		bsmStringHead := BSMStringHead{}
		check(binary.Read(bsmFile, binary.LittleEndian, &bsmStringHead))

		stringData := make([]byte, bsmStringHead.Size)
		check(binary.Read(bsmFile, binary.LittleEndian, &stringData))

		lang := langNames[bsmStringHead.Language]
		section := uint8(math.Log2(float64(bsmStringHead.SectionID)))
		//stringId := uint16(math.Log2(float64(bsmStringHeader.StringID)))

		utf8StringData, _, err := transform.Bytes(charmap.Windows1252.NewDecoder(), stringData[:len(stringData)-1])
		check(err)

		languages[lang][section] = append(languages[lang][section], string(utf8StringData))
	}

	jsonFile, err := os.Create(jsonPath)
	check(err)
	defer jsonFile.Close()

	jsonEncoder := json.NewEncoder(jsonFile)
	jsonEncoder.SetIndent("", "\t")

	err = jsonEncoder.Encode(languages)
	check(err)
}

func jsonToBsm(jsonPath string, bsmPath string) {
	jsonFile, err := os.Open(jsonPath)
	check(err)

	jsonDecoder := json.NewDecoder(jsonFile)

	var languages map[string]map[uint8][]string
	err = jsonDecoder.Decode(&languages)
	check(err)

	jsonFile.Close()

	var strings []BSMString

	for _, lang := range langNames {
		for sectionID, stringList := range languages[lang] {
			for stringIdx, str := range stringList {
				stringData := []byte(str)
				stringData, _, err = transform.Bytes(charmap.Windows1252.NewEncoder(), stringData)
				check(err)

				stringData = append(stringData, 0)

				stringHead := BSMStringHead{
					Language:  langIds[lang],
					StringID:  uint16(1 << stringIdx),
					SectionID: 1 << sectionID,
					Size:      uint32(len(stringData)),
				}

				strings = append(strings, BSMString{
					header: stringHead,
					data:   stringData,
				})
			}
		}
	}

	bsmFile, err := os.Create(bsmPath)
	check(err)
	defer bsmFile.Close()

	bsmHead := BSMHeader{
		Magic: BSMMagic,
		Size:  uint32(len(strings)),
	}

	check(binary.Write(bsmFile, binary.LittleEndian, &bsmHead))

	for _, str := range strings {
		check(binary.Write(bsmFile, binary.LittleEndian, &str.header))
		check(binary.Write(bsmFile, binary.LittleEndian, &str.data))
	}
}
