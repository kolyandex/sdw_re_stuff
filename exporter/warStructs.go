package main

type WarHeader struct {
	Crc32                             uint32
	Version                           [4]byte
	FogRed, FogGreen, FogBlue, Unused byte
	ResourceCount                     uint32
}

type WarModel struct {
	VerticesOffset     uint32
	MeshHeadOffset     uint32
	VerticesCount      uint16
	MeshHeadCount      uint16
	CollisionBoxOffset uint32 // optional, none if 0
}

type WarAnimatedModel struct {
	AnimListOffset uint32
	SkeletonOffset uint32
	BoneCount      uint16
	Padding        uint16
}

type WarSkeletonBone struct {
	ParentBoneIndex uint16
	X, Y, Z         int16
	VerticesCount   uint16
}

type WarVertex struct {
	X, Y, Z, Unused int16
}

type WarColor struct {
	Red, Green, Blue, Unused byte
}

type WarUV struct {
	U, V byte
}

type WarPolygonChunkHeader struct {
	Type  int16
	Count uint16
}

type WarScenaricResource struct {
	ModelResourceIndex        uint16
	LowPolyModelResourceIndex uint16
	X                         int16
	Y                         int16
	Z                         int16
	ClassID                   uint16
	Pitch                     int16
	Yaw                       int16
	Roll                      int16
	Padding                   uint16
}

type WarExportHeader struct {
	Id    uint16
	Count uint16
}

type WarCollisionMapHeader struct {
	GridWidth  uint16
	GridHeight uint16
	WorldMinX  int16
	WorldMinY  int16
	CellWidth  int16
	CellHeight int16
}

type WarCollisionVertex struct {
	X, Y, Z int16
}

type WarCollisionPolygon struct {
	BoxMinX int16
	BoxMinY int16
	BoxMinZ int16
	BoxMaxX int16
	BoxMaxY int16
	BoxMaxZ int16

	NormalX int16
	NormalY int16
	NormalZ int16

	SurfaceFlags int16

	Vertices [3]WarCollisionVertex

	Pad uint16
}

type WarBox struct {
	X1 int16
	Y1 int16
	Z1 int16
	X2 int16
	Y2 int16
	Z2 int16
}

type WarAnimation struct {
	Name       [8]uint8
	FrameCount uint16
}

type WarAnimationFrame struct {
	FrameDuration uint16
	NumTransforms uint16
	DataSize      uint16
	SoundId       uint16
}

type WarAnimationTransformFlags struct {
	BoneIndex uint8 // Bits 0-5
	HasRotX   bool  // Bit 6
	HasRotY   bool  // Bit 7
	HasRotZ   bool  // Bit 8
	HasPosX   bool  // Bit 9
	HasPosY   bool  // Bit 10
	HasPosZ   bool  // Bit 11
	HasSclX   bool  // Bit 12
	HasSclY   bool  // Bit 13
	HasSclZ   bool  // Bit 14
	Is8Bit    bool  // Bit 15 (0 = 16-bit High Prec, 1 = 8-bit Low Prec)
}

func parseWarAnimationTransformFlags(flags uint16) WarAnimationTransformFlags {
	return WarAnimationTransformFlags{
		BoneIndex: uint8(flags & 0x3F),

		HasRotX: (flags & 0x0040) != 0,
		HasRotY: (flags & 0x0080) != 0,
		HasRotZ: (flags & 0x0100) != 0,

		HasPosX: (flags & 0x0200) != 0,
		HasPosY: (flags & 0x0400) != 0,
		HasPosZ: (flags & 0x0800) != 0,

		HasSclX: (flags & 0x1000) != 0,
		HasSclY: (flags & 0x2000) != 0,
		HasSclZ: (flags & 0x4000) != 0,

		Is8Bit: (flags & 0x8000) != 0,
	}
}

func (f WarAnimationTransformFlags) transformSize() int {
	count := 0
	if f.HasRotX {
		count++
	}
	if f.HasRotY {
		count++
	}
	if f.HasRotZ {
		count++
	}
	if f.HasPosX {
		count++
	}
	if f.HasPosY {
		count++
	}
	if f.HasPosZ {
		count++
	}
	if f.HasSclX {
		count++
	}
	if f.HasSclY {
		count++
	}
	if f.HasSclZ {
		count++
	}

	if f.Is8Bit == false {
		count *= 2
	}

	return count
}

func (f WarAnimationTransformFlags) isEmpty() bool {
	return f.transformSize() == 0
}
