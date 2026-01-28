package main

import (
	"bytes"
	"encoding/binary"
	"fmt"
	"image"
	"image/color"
	"image/png"
	"io"
	"os"
	"path/filepath"
)

type DAV struct {
	Magic        [4]uint8
	Padding      [4]uint32
	HeaderOffset uint32
}

type DAVHeader struct {
	MaterialSize uint16
	TextureSize  uint16
	PageSize     uint16

	MaterialOffset uint32
	TextureOffset  uint32
	PageOffset     uint32
	ExpTexOffset   uint32
}

type DAVTexture struct {
	X         int16
	Width     int16
	Y         int16
	Height    int16
	PageIndex uint16
}

type DAVExportTex struct {
	ID   uint16
	Size uint16
}

type TextureAtlasHeader struct {
	X1 int16
	X2 int16
	Y1 int16
	Y2 int16

	Flags uint16
}

type BitmapFileHeader struct {
	Type      [2]uint8
	Size      uint32
	Reserved1 uint16
	Reserved2 uint16
	OffBits   uint32
}

type BitmapInfoHeaderV3 struct {
	Size          uint32
	Width         int32
	Height        int32
	Planes        uint16
	BitCount      uint16
	Compression   uint32
	SizeImage     uint32
	XPelsPerMeter int32
	YPelsPerMeter int32
	ClrUsed       uint32
	ClrImportant  uint32
	RedMask       uint32
	GreenMask     uint32
	BlueMask      uint32
	AlphaMask     uint32
}

type CIEXYZ struct {
	X, Y, Z int32 // FXPT2DOT30
}

type CIEXYZTriple struct {
	Red, Green, Blue CIEXYZ
}

type BitmapInfoHeaderV4 struct {
	Size          uint32
	Width         int32
	Height        int32
	Planes        uint16
	BitCount      uint16
	Compression   uint32
	SizeImage     uint32
	XPelsPerMeter int32
	YPelsPerMeter int32
	ClrUsed       uint32
	ClrImportant  uint32
	RedMask       uint32
	GreenMask     uint32
	BlueMask      uint32
	AlphaMask     uint32
	// V4 specific fields (52 bytes)
	CSType     uint32
	Endpoints  CIEXYZTriple
	GammaRed   uint32
	GammaGreen uint32
	GammaBlue  uint32
}

var exportTexNames = map[uint16]string{
	11:  "I01LVU4_",
	12:  "I01LVU5_",
	13:  "IDCICONA",
	14:  "IDCICONB",
	15:  "IDYICONA",
	16:  "IDYICONB",
	17:  "IFUICONA",
	18:  "IFUICONB",
	19:  "IGLCADG_",
	20:  "IGLCADH_",
	21:  "IGLCADP_",
	22:  "IGLCERC_",
	23:  "IGLCOMBA",
	24:  "IGLCOMBB",
	25:  "IGLCRAI1",
	26:  "IGLEXITA",
	27:  "IGLEXITB",
	28:  "IGLFLEC_",
	29:  "IGLFONTE",
	30:  "IGLHELPA",
	31:  "IGLHELPB",
	32:  "IGLNEIG1",
	33:  "IGLOMBR1",
	34:  "IGLSMOK_",
	35:  "IGLTRIA_",
	36:  "IGLTYPO_",
	37:  "IGLUSEDA",
	38:  "IGLUSEDB",
	39:  "IGLVOL0_",
	40:  "IGLVOL1_",
	41:  "IGLVOL2_",
	42:  "IGLVOL3_",
	43:  "IGLVOL4_",
	44:  "ILAICONA",
	45:  "ILAICONB",
	46:  "IMEMCMAP",
	47:  "IMOICONA",
	48:  "IMOICONB",
	49:  "IVEICONA",
	50:  "IVEICONB",
	51:  "MAP",
	55:  "IGLCRAI2",
	56:  "IENVMAP",
	57:  "IGLTIDE_",
	58:  "IFTICONA",
	59:  "IFTICONB",
	60:  "IFLICONA",
	61:  "IFLICONB",
	62:  "IGLBULL_",
	63:  "IFLFUMEE",
	65:  "IGAICONA",
	66:  "IGAICONB",
	67:  "IC4ICONA",
	68:  "IC4ICONB",
	71:  "ICTJAUG1",
	72:  "ICHICONB",
	74:  "ICHICONA",
	75:  "IMNICONA",
	76:  "IMNICONB",
	77:  "IMNINT1_",
	78:  "IMNINT2_",
	79:  "IEAICONA",
	80:  "IEAICONB",
	81:  "IMNCERC_",
	83:  "IMNPADS_",
	84:  "IMNPADC_",
	85:  "IMNPADT_",
	86:  "IMNPADX_",
	87:  "IGLEAVE1",
	89:  "IMNINT3_",
	91:  "IMNCHEK_",
	93:  "IAPICONA",
	94:  "IAPICONB",
	95:  "IFLOCO01",
	96:  "IAIICONA",
	97:  "IAIICONB",
	101: "IDUICONB",
	102: "IPRICONB",
	103: "IPRICONA",
	105: "ICLICONA",
	106: "ICLICONB",
	107: "IDTICONA",
	108: "IDTICONB",
	109: "ICPICONA",
	110: "ICPICONB",
	111: "ICPICONC",
	112: "IDTICONC",
	113: "IDUICONA",
	114: "IPMICONA",
	115: "IPMICONB",
	117: "IMGICONA",
	118: "IMGICONB",
	120: "IFFICONA",
	121: "IFFICONB",
	122: "IPASNE01",
	126: "ICFICONA",
	127: "ICFICONB",
	130: "ITICPTR_",
	131: "IFUJAUG_",
	132: "IFUJIC1_",
	133: "IFUJIC2_",
	134: "ITITRE01",
	135: "ITITRE02",
	138: "ITRICONA",
	139: "ITRICONB",
	140: "ITITRE03",
	141: "ITITRE04",
	142: "IGLCOYO1",
	143: "ISTART01",
	144: "ISTART02",
	145: "ISTART03",
	146: "ISTART04",
	152: "ITPDESS_",
	155: "IASPILEA",
	156: "IASPILEB",
	157: "IASICONA",
	158: "IASICONB",
	160: "ICSMASQ_",
	162: "ICPICOND",
	163: "IORICONA",
	164: "IORICONB",
	171: "IGHICONA",
	172: "IGHICONB",
	174: "D0XCADR_",
	193: "IGHICONC",
	194: "IDTJAUG_",
	207: "IFAOREO_",
	208: "IFBSMOK_",
	209: "IFTNOTE_",
	210: "IFTTSAM_",
	214: "IMDANSE1",
	218: "IMDANSE2",
	219: "IMDANSE3",
	220: "IMDANSE4",
	225: "IGLGOUT1",
	229: "IBOCKOI_",
	230: "IBOGAGN_",
	232: "IPASNE02",
	233: "IGLGOUT2",
	235: "IMEMCICA",
	236: "IMEMCICB",
	237: "IGLALFA_",
	238: "IGLSMO2_",
	239: "IGLLIGF_",
	240: "IGLLIGB_",
}

var DAVMagic = [4]uint8{'V', 'D', 'X', '7'}

func getColorMasks(RGBA4444 bool) (red uint16, green uint16, blue uint16, alpha uint16) {
	if RGBA4444 {
		red = 0xf00
		green = 0xf0
		blue = 0xf
		alpha = 0xf000
	} else {
		red = 0x7c00
		green = 0x3e0
		blue = 0x1f
		alpha = 0x8000
	}
	return
}

func davExtract(path string, outPath string) {
	file, err := os.Open(path)
	check(err)
	defer file.Close()

	dav := DAV{}
	binary.Read(file, binary.LittleEndian, &dav)

	if dav.Magic != DAVMagic {
		panic("invalid DAV magic")
	}

	err = os.MkdirAll(outPath, 0755)
	check(err)

	_, err = file.Seek(int64(dav.HeaderOffset), 0)
	check(err)

	davHeader := DAVHeader{}
	binary.Read(file, binary.LittleEndian, &davHeader)

	_, err = file.Seek(int64(davHeader.TextureOffset), 0)
	check(err)

	textures := make([]DAVTexture, davHeader.TextureSize)
	binary.Read(file, binary.LittleEndian, &textures)

	_, err = file.Seek(int64(davHeader.ExpTexOffset), 0)
	check(err)

	var exportTexNum uint32
	binary.Read(file, binary.LittleEndian, &exportTexNum)

	texToExpTexName := make(map[uint16]string)
	for range exportTexNum {
		expTex := DAVExportTex{}
		binary.Read(file, binary.LittleEndian, &expTex)

		offsets := make([]uint32, expTex.Size)
		binary.Read(file, binary.LittleEndian, &offsets)

		expTexName, exists := exportTexNames[expTex.ID]
		if !exists {
			expTexName = fmt.Sprintf("%d", expTex.ID)
		}

		currentOffset, err := file.Seek(0, 1)
		check(err)

		for j := range len(offsets) {
			offset := offsets[j]

			var newExpTexName string
			if j == 0 {
				newExpTexName = expTexName
			} else {
				newExpTexName = fmt.Sprintf("%s_%d", expTexName, j)
			}

			if offset >= davHeader.TextureOffset {
				textureIdx := uint16((offset - davHeader.TextureOffset) / 0xa)
				_, texIdxExists := texToExpTexName[textureIdx]
				if texIdxExists {
					continue
				}
				texToExpTexName[textureIdx] = newExpTexName
			} else {
				_, err = file.Seek(int64(offset), 0)
				check(err)

				var textureIdx uint16
				binary.Read(file, binary.LittleEndian, &textureIdx)

				_, texIdxExists := texToExpTexName[textureIdx]
				if texIdxExists {
					continue
				}

				texToExpTexName[textureIdx] = newExpTexName
			}
		}

		_, err = file.Seek(currentOffset, 0)
		check(err)
	}

	_, err = file.Seek(int64(davHeader.PageOffset), 0)
	check(err)

	for i := range davHeader.PageSize {
		atlasHeader := TextureAtlasHeader{}
		binary.Read(file, binary.LittleEndian, &atlasHeader)
		//fmt.Printf("%+v\n", atlasHeader)

		width := atlasHeader.X1 - atlasHeader.X2
		if width < 0 {
			width = -width
		}
		height := atlasHeader.Y1 - atlasHeader.Y2
		if height < 0 {
			height = -height
		}

		//fmt.Printf("width: %d, height: %d\n", width, height)

		atlasData := make([][]uint16, height)
		for y := range height {
			atlasData[y] = make([]uint16, width)
			binary.Read(file, binary.LittleEndian, atlasData[y])
		}

		redMask, greenMask, blueMask, alphaMask := getColorMasks((atlasHeader.Flags & 0x1c) != 4)

		for y := range height {
			for x := range width {
				atlasData[y][x] ^= alphaMask
			}
		}

		for j := range davHeader.TextureSize {
			texture := textures[j]
			if texture.PageIndex != i {
				continue
			}

			texSize := uint32(texture.Width) * uint32(texture.Height)

			// stupid ass bmp padding
			if ((texSize * 2) % 4) != 0 {
				texSize++
			}

			texSizeBytes := texSize * 2

			texData := make([]uint16, texSize)

			texOffset := 0
			for y := range texture.Height {
				for x := range texture.Width {
					texData[texOffset] = atlasData[y+texture.Y][x+texture.X]
					texOffset++
				}
			}

			bmpFileHeader := BitmapFileHeader{
				Type:      [2]uint8{0x42, 0x4D}, // BM
				Size:      14 + 108 + texSizeBytes,
				Reserved1: 0,
				Reserved2: 0,
				OffBits:   70,
			}

			bmpInfoHeader := BitmapInfoHeaderV4{
				Size:          108,
				Width:         int32(texture.Width),
				Height:        -int32(texture.Height),
				Planes:        1,
				BitCount:      16,
				Compression:   0x3, // BI_BITFIELDS
				SizeImage:     texSizeBytes,
				XPelsPerMeter: 0,
				YPelsPerMeter: 0,
				ClrUsed:       0,
				ClrImportant:  0,
				RedMask:       uint32(redMask),
				GreenMask:     uint32(greenMask),
				BlueMask:      uint32(blueMask),
				AlphaMask:     uint32(alphaMask),
				CSType:        0x73524742,
				Endpoints:     CIEXYZTriple{},
				GammaRed:      0,
				GammaGreen:    0,
				GammaBlue:     0,
			}

			var filename string
			expTexName, exists := texToExpTexName[j]
			if exists {
				filename = fmt.Sprintf("%s.BMP", expTexName)
			} else {
				filename = fmt.Sprintf("%d.bmp", j)
			}

			func() {
				bmpFile, err := os.Create(filepath.Join(outPath, filename))
				check(err)
				defer bmpFile.Close()

				binary.Write(bmpFile, binary.LittleEndian, bmpFileHeader)
				binary.Write(bmpFile, binary.LittleEndian, bmpInfoHeader)
				binary.Write(bmpFile, binary.LittleEndian, texData)
			}()
		}
	}
}

func davExtractPages(path string, outPath string) {
	file, err := os.Open(path)
	check(err)
	defer file.Close()

	dav := DAV{}
	binary.Read(file, binary.LittleEndian, &dav)

	if dav.Magic != DAVMagic {
		panic("invalid DAV magic")
	}

	err = os.MkdirAll(outPath, 0755)
	check(err)

	_, err = file.Seek(int64(dav.HeaderOffset), 0)
	check(err)

	davHeader := DAVHeader{}
	binary.Read(file, binary.LittleEndian, &davHeader)

	_, err = file.Seek(int64(davHeader.PageOffset), 0)
	check(err)

	for i := range davHeader.PageSize {
		atlasHeader := TextureAtlasHeader{}
		binary.Read(file, binary.LittleEndian, &atlasHeader)
		//fmt.Printf("%+v\n", atlasHeader)

		width := atlasHeader.X1 - atlasHeader.X2
		if width < 0 {
			width = -width
		}
		height := atlasHeader.Y1 - atlasHeader.Y2
		if height < 0 {
			height = -height
		}

		//fmt.Printf("width: %d, height: %d\n", width, height)

		atlasSize := uint32(width) * uint32(height)

		// stupid ass bmp padding
		atlasBmpSize := atlasSize
		if ((atlasBmpSize * 2) % 4) != 0 {
			atlasBmpSize++
		}

		atlasData := make([]uint16, atlasBmpSize)
		binary.Read(file, binary.LittleEndian, atlasData[:atlasSize])

		atlasSizeBytes := atlasBmpSize * 2

		redMask, greenMask, blueMask, alphaMask := getColorMasks((atlasHeader.Flags & 0x1c) != 4)

		for j := range atlasSize {
			atlasData[j] ^= alphaMask
		}

		bmpFileHeader := BitmapFileHeader{
			Type:      [2]uint8{0x42, 0x4D}, // BM
			Size:      14 + 108 + atlasSizeBytes,
			Reserved1: 0,
			Reserved2: 0,
			OffBits:   70,
		}

		bmpInfoHeader := BitmapInfoHeaderV4{
			Size:          108,
			Width:         int32(width),
			Height:        -int32(height),
			Planes:        1,
			BitCount:      16,
			Compression:   0x3, // BI_BITFIELDS
			SizeImage:     atlasSizeBytes,
			XPelsPerMeter: 0,
			YPelsPerMeter: 0,
			ClrUsed:       0,
			ClrImportant:  0,
			RedMask:       uint32(redMask),
			GreenMask:     uint32(greenMask),
			BlueMask:      uint32(blueMask),
			AlphaMask:     uint32(alphaMask),
			CSType:        0x73524742,
			Endpoints:     CIEXYZTriple{},
			GammaRed:      0,
			GammaGreen:    0,
			GammaBlue:     0,
		}

		filename := fmt.Sprintf("%d.bmp", i)

		func() {
			bmpFile, err := os.Create(filepath.Join(outPath, filename))
			check(err)
			defer bmpFile.Close()

			binary.Write(bmpFile, binary.LittleEndian, bmpFileHeader)
			binary.Write(bmpFile, binary.LittleEndian, bmpInfoHeader)
			binary.Write(bmpFile, binary.LittleEndian, atlasData)
		}()
	}
}

func getMaterialInfo(dav *os.File, materialIdx uint16) DAVTexture {
	_, err := dav.Seek(0, io.SeekStart)
	check(err)

	davHead := DAV{}
	check(binary.Read(dav, binary.LittleEndian, &davHead))

	if davHead.Magic != DAVMagic {
		panic("invalid DAV magic")
	}

	_, err = dav.Seek(int64(davHead.HeaderOffset), 0)
	check(err)

	davHeader := DAVHeader{}
	check(binary.Read(dav, binary.LittleEndian, &davHeader))

	if materialIdx >= davHeader.MaterialSize {
		panic("material index too high")
	}

	_, err = dav.Seek(int64(davHeader.MaterialOffset)+int64(materialIdx*2), 0)
	check(err)

	var material uint16
	check(binary.Read(dav, binary.LittleEndian, &material))

	_, err = dav.Seek(int64(davHeader.TextureOffset)+int64(material*10), 0)
	check(err)

	textureInfo := DAVTexture{}
	check(binary.Read(dav, binary.LittleEndian, &textureInfo))

	return textureInfo
}

// ToRGBA converts a uint16 pixel value into a color.RGBA object.
func ToRGBA(val uint16, RGBA4444 bool) color.NRGBA {
	if RGBA4444 {
		// RGBA 4444: 4 bits per channel
		// R: 0x0f00, G: 0x00f0, B: 0x000f, A: 0xf000
		r := uint8((val & 0x0f00) >> 8)
		g := uint8((val & 0x00f0) >> 4)
		b := uint8(val & 0x000f)
		a := uint8((val & 0xf000) >> 12)
		a = a ^ 0xf

		// Scale 4-bit (0-15) to 8-bit (0-255)
		// Bitwise trick: (v << 4) | v  is equivalent to v * 17
		return color.NRGBA{
			R: (r << 4) | r,
			G: (g << 4) | g,
			B: (b << 4) | b,
			A: (a << 4) | a,
		}
	}

	// Format: 5551 (15-bit color + 1-bit alpha)
	// R: 0x7c00, G: 0x03e0, B: 0x001f, A: 0x8000
	r := uint8((val & 0x7c00) >> 10)
	g := uint8((val & 0x03e0) >> 5)
	b := uint8(val & 0x001f)
	a := uint8((val & 0x8000) >> 15)
	a = a ^ 0x1

	// Scale 5-bit (0-31) to 8-bit (0-255)
	// Bitwise trick: (v << 3) | (v >> 2)
	// Scale 1-bit (0-1) to 8-bit (0-255)
	var alpha uint8
	if a > 0 {
		alpha = 255
	}

	return color.NRGBA{
		R: (r << 3) | (r >> 2),
		G: (g << 3) | (g >> 2),
		B: (b << 3) | (b >> 2),
		A: alpha,
	}
}

func getAtlasPng(dav *os.File, atlasIndex uint16) []byte {
	_, err := dav.Seek(0, io.SeekStart)
	check(err)

	davHead := DAV{}
	check(binary.Read(dav, binary.LittleEndian, &davHead))

	if davHead.Magic != DAVMagic {
		panic("invalid DAV magic")
	}

	_, err = dav.Seek(int64(davHead.HeaderOffset), 0)
	check(err)

	davHeader := DAVHeader{}
	check(binary.Read(dav, binary.LittleEndian, &davHeader))

	if atlasIndex >= davHeader.PageSize {
		panic("atlas index too high")
	}

	_, err = dav.Seek(int64(davHeader.PageOffset), 0)
	check(err)

	for i := range davHeader.PageSize {
		atlasHeader := TextureAtlasHeader{}
		check(binary.Read(dav, binary.LittleEndian, &atlasHeader))

		width := atlasHeader.X1 - atlasHeader.X2
		if width < 0 {
			width = -width
		}
		height := atlasHeader.Y1 - atlasHeader.Y2
		if height < 0 {
			height = -height
		}

		atlasSize := uint32(width) * uint32(height)

		if i != atlasIndex {
			_, err = dav.Seek(int64(atlasSize*2), io.SeekCurrent)
			check(err)
			continue
		}

		img := image.NewNRGBA(image.Rect(0, 0, int(width), int(height)))

		for y := range int(height) {
			for x := range int(width) {
				var pixel uint16
				check(binary.Read(dav, binary.LittleEndian, &pixel))
				img.Set(x, y, ToRGBA(pixel, (atlasHeader.Flags&0x1c) != 4))
			}
		}

		pngBuffer := new(bytes.Buffer)
		check(png.Encode(pngBuffer, img))

		return pngBuffer.Bytes()
	}

	panic("bruh")
}
