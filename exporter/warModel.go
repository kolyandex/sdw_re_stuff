package main

import (
	"encoding/binary"
	"fmt"
	"io"
	"os"
)

type Position struct {
	X, Y, Z int16
}

type Vertex struct {
	position Position
	color    [3]byte

	materialIndex uint16
	uv            WarUV

	boneIndex uint16
}

type MeshPrimitive struct {
	vertices []Vertex
	indices  []uint16
}

type MeshPrimitives struct {
	vertexColored MeshPrimitive
	textured      map[uint16]MeshPrimitive
}

type Animation struct {
	name   string
	frames []AnimationFrame
}

type AnimationFrame struct {
	frameDuration  uint16
	transformCount uint16
	transformData  []byte
}

type Model struct {
	textured      map[uint16]GltfMeshPrimitive
	vertexColored GltfMeshPrimitive

	bones      []WarSkeletonBone
	animations []Animation
}

type ModelExporter struct {
	war *os.File
	dav *os.File

	positions   []Position
	boneIndexes []uint16
	primitives  MeshPrimitives

	bones      []WarSkeletonBone
	animations []Animation

	isAnimated bool

	materialCache map[uint16]DAVTexture
}

func (e *ModelExporter) init(model WarModel) {
	e.positions = make([]Position, model.VerticesCount)
	e.bones = []WarSkeletonBone{}
	e.animations = []Animation{}
	e.boneIndexes = []uint16{}
	e.primitives.vertexColored = MeshPrimitive{}
	e.primitives.textured = make(map[uint16]MeshPrimitive)
	if e.materialCache == nil {
		e.materialCache = make(map[uint16]DAVTexture)
	}
	e.isAnimated = false
}

func clen(n [8]byte) int {
	for i := 0; i < len(n); i++ {
		if n[i] == 0 {
			return i
		}
	}
	return len(n)
}

func (e *ModelExporter) getModel(resourceOffset uint32, modelType uint8) Model {
	_, err := e.war.Seek(int64(resourceOffset), io.SeekStart)
	check(err)

	model := WarModel{}
	check(binary.Read(e.war, binary.LittleEndian, &model))

	e.init(model)

	if modelType == 4 {
		animatedModel := WarAnimatedModel{}
		check(binary.Read(e.war, binary.LittleEndian, &animatedModel))

		_, err = e.war.Seek(int64(animatedModel.SkeletonOffset), io.SeekStart)
		check(err)

		e.bones = make([]WarSkeletonBone, animatedModel.BoneCount)
		check(binary.Read(e.war, binary.LittleEndian, &e.bones))

		for i, bone := range e.bones {
			for range bone.VerticesCount {
				e.boneIndexes = append(e.boneIndexes, uint16(i))
			}
		}

		_, err = e.war.Seek(int64(animatedModel.AnimListOffset), io.SeekStart)
		check(err)

		var animCount uint32
		check(binary.Read(e.war, binary.LittleEndian, &animCount))

		animOffsets := make([]uint32, animCount)
		check(binary.Read(e.war, binary.LittleEndian, &animOffsets))

		for _, offset := range animOffsets {
			if offset == 0 {
				continue
			}

			_, err = e.war.Seek(int64(offset), io.SeekStart)
			check(err)

			warAnimation := WarAnimation{}
			check(binary.Read(e.war, binary.LittleEndian, &warAnimation))

			animation := Animation{
				name:   string(warAnimation.Name[:clen(warAnimation.Name)]),
				frames: make([]AnimationFrame, warAnimation.FrameCount),
			}

			for i := range warAnimation.FrameCount {
				warFrame := WarAnimationFrame{}
				check(binary.Read(e.war, binary.LittleEndian, &warFrame))

				frame := AnimationFrame{
					frameDuration:  warFrame.FrameDuration,
					transformCount: warFrame.NumTransforms,
					transformData:  make([]byte, warFrame.DataSize*2),
				}

				check(binary.Read(e.war, binary.LittleEndian, &frame.transformData))

				animation.frames[i] = frame
			}

			e.animations = append(e.animations, animation)
		}

		e.isAnimated = true
	}

	_, err = e.war.Seek(int64(model.VerticesOffset), io.SeekStart)
	check(err)

	warVertices := make([]WarVertex, model.VerticesCount)
	check(binary.Read(e.war, binary.LittleEndian, &warVertices))

	for i, vertex := range warVertices {
		e.positions[i] = Position{
			X: vertex.X,
			Y: vertex.Y,
			Z: vertex.Z,
		}
	}

	_, err = e.war.Seek(int64(model.MeshHeadOffset), io.SeekStart)
	check(err)

	for range model.MeshHeadCount {
		chunkHeader := WarPolygonChunkHeader{}
		check(binary.Read(e.war, binary.LittleEndian, &chunkHeader))

		skip := false

		for range chunkHeader.Count {
			switch chunkHeader.Type {
			case 0:
				e.handleColorTriangle()
			case 1:
				e.handleColorQuad()
			case 2:
				e.handleColorBlendedTriangle()
			case 3:
				e.handleColorBlendedQuad()
			case 4, 20:
				e.handleTexturedTriangle()
			case 5:
				e.handleTexturedQuad()
			case 6, 0x15:
				e.handleColorBlendedTexturedTriangle()
			case 7:
				e.handleColorBlendedTexturedQuad()
			case 8, 9, 10, 11:
				var uvScale byte
				switch chunkHeader.Type {
				case 0x8, 0x9:
					uvScale = 64
				case 0xA, 0xB:
					uvScale = 32
				}

				var uvs [4]WarUV
				switch chunkHeader.Type {
				case 0x8, 0xA:
					uvs = [4]WarUV{
						{0, 0}, {0, uvScale},
						{uvScale, 0}, {uvScale, uvScale},
					}
				case 0x9, 0xB:
					uvs = [4]WarUV{
						{0, 0}, {uvScale, 0},
						{0, uvScale}, {uvScale, uvScale},
					}
				}
				e.handleColorBlendedTexturedQuadWithSquareUV(uvs)
			case 12, 13, 14, 15:
				var uvScale byte
				switch chunkHeader.Type {
				case 0xC, 0xD:
					uvScale = 64
				case 0xE, 0xF:
					uvScale = 32
				}

				var uvs [4]WarUV
				switch chunkHeader.Type {
				case 0xC, 0xE:
					uvs = [4]WarUV{
						{0, 0}, {0, uvScale},
						{uvScale, 0}, {uvScale, uvScale},
					}
				case 0xD, 0xF:
					uvs = [4]WarUV{
						{0, 0}, {uvScale, 0},
						{0, uvScale}, {uvScale, uvScale},
					}
				}

				e.handleTexturedQuadWithSquareUV(uvs)
			case 0x12, 0x16:
				e.handleColorBlendedTriangleWithAlphaMode()
			case 0x13:
				e.handleColorBlendedQuadWithAlphaMode()
			default:
				fmt.Printf("found chunk type %d, skipping...\n", chunkHeader.Type)
				skip = true
			}

			if skip {
				break
			}
		}

		if skip {
			break
		}
	}

	texturedGltfPrimitives := make(map[uint16]GltfMeshPrimitive, len(e.primitives.textured))
	for atlasIndex, primitive := range e.primitives.textured {
		texturedGltfPrimitives[atlasIndex] = e.makeGltfMeshPrimitive(primitive, e.dav != nil)
	}

	return Model{
		textured:      texturedGltfPrimitives,
		vertexColored: e.makeGltfMeshPrimitive(e.primitives.vertexColored, false),

		bones:      e.bones,
		animations: e.animations,
	}
}
