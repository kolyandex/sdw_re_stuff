package main

import (
	"bytes"
	"encoding/binary"
	"fmt"
	"io"
	"maps"
	"math"
	"os"
	"slices"
	"strconv"

	"github.com/qmuntal/gltf"
	"github.com/qmuntal/gltf/ext/unlit"
	"github.com/qmuntal/gltf/modeler"
)

func linearize(v float32) float32 {
	if v <= 0.04045 {
		return v / 12.92
	}
	return float32(math.Pow(float64(v+0.055)/1.055, 2.4))
}

func calculateTextureUV(uv WarUV, textureInfo DAVTexture) [2]float32 {
	result := [2]float32{}

	for i, coord := range [2]byte{uv.U, uv.V} {
		textureSize := float32(textureInfo.Width)
		if i == 1 {
			textureSize = float32(textureInfo.Height)
		}

		textureCoord := float32(textureInfo.X)
		if i == 1 {
			textureCoord = float32(textureInfo.Y)
		}

		atlasCoord := ((textureSize - 1) / textureSize) * float32(coord)
		atlasCoord += 0.5
		atlasCoord += textureCoord
		atlasCoord /= 256.0

		result[i] = atlasCoord
	}

	return result
}

type GltfMeshPrimitive struct {
	positions   [][3]float32
	indices     []uint16
	colors      [][3]float32
	boneIndexes []uint16
	uvCoords    [][2]float32
}

func (e *ModelExporter) makeGltfMeshPrimitive(meshPrimitive MeshPrimitive, isTextured bool) GltfMeshPrimitive {
	numVertices := len(meshPrimitive.vertices)

	gltfMeshPrimitive := GltfMeshPrimitive{
		positions: make([][3]float32, numVertices),
		indices:   meshPrimitive.indices,
		colors:    make([][3]float32, numVertices),
	}

	if e.isAnimated {
		gltfMeshPrimitive.boneIndexes = make([]uint16, numVertices)
	}

	if isTextured {
		gltfMeshPrimitive.uvCoords = make([][2]float32, numVertices)
	}

	for i, vertex := range meshPrimitive.vertices {
		gltfMeshPrimitive.positions[i] = [3]float32{
			float32(vertex.position.X) / WorldScale,
			-float32(vertex.position.Y) / WorldScale,
			-float32(vertex.position.Z) / WorldScale,
		}
		/*if isTextured {
			for i, color := range vertex.color {
				bigColor := float32(color)
				bigColor *= 2
				vertex.color[i] = uint8(min(bigColor, 255.0))
			}
		}*/
		/*if !isTextured {
			for i, color := range vertex.color {
				vertex.color[i] = color >> 1
			}
		}*/

		newColor := [3]float32{
			float32(vertex.color[0]) / 255,
			float32(vertex.color[1]) / 255,
			float32(vertex.color[2]) / 255,
		}

		// TODO: color values can be over 1.0 for textured polygons
		// this isn't supported by the gltf spec, so it results in errors when validating the gltf
		// and some editors/viewers will probably clamp this down to 1.0
		// honestly i'm not sure how to fix this...
		if isTextured {
			for i := range newColor {
				newColor[i] *= 1.95
			}
		}

		gltfMeshPrimitive.colors[i] = [3]float32{
			linearize(newColor[0]),
			linearize(newColor[1]),
			linearize(newColor[2]),
		}
		/*if isTextured {
			for j, color := range gltfMeshPrimitive.colors[i] {
				gltfMeshPrimitive.colors[i][j] = color * 4.4
			}
		}*/

		if e.isAnimated {
			gltfMeshPrimitive.boneIndexes[i] = vertex.boneIndex
		}

		if isTextured {
			textureInfo, ok := e.materialCache[vertex.materialIndex]
			if !ok {
				panic("this should not happen")
			}

			gltfMeshPrimitive.uvCoords[i] = calculateTextureUV(vertex.uv, textureInfo)
		}
	}

	return gltfMeshPrimitive
}

func BuildSkinningAttributes(boneIndexes []uint16) (joints [][4]uint16, weights [][4]float32) {
	joints = make([][4]uint16, len(boneIndexes))
	weights = make([][4]float32, len(boneIndexes))

	for i, boneIndex := range boneIndexes {
		joints[i] = [4]uint16{boneIndex, 0, 0, 0}
		weights[i] = [4]float32{1.0, 0.0, 0.0, 0.0}
	}

	return
}

type GltfPrimitiveAdder struct {
	doc                 *gltf.Document
	dav                 *os.File
	materialCache       map[uint16]int
	vertexColorMaterial *int
	samplerIndex        *int
}

func (a *GltfPrimitiveAdder) addGltfMeshPrimitive(mesh *gltf.Mesh, gltfMeshPrimitive GltfMeshPrimitive, atlasIndex *uint16) {
	if len(gltfMeshPrimitive.positions) == 0 {
		return
	}

	positionAccessor := modeler.WritePosition(a.doc, gltfMeshPrimitive.positions)
	indicesAccessor := modeler.WriteIndices(a.doc, gltfMeshPrimitive.indices)

	colorIndices := modeler.WriteColor(a.doc, gltfMeshPrimitive.colors)

	attributes := gltf.PrimitiveAttributes{
		gltf.POSITION: positionAccessor,
		gltf.COLOR_0:  colorIndices,
	}

	if len(gltfMeshPrimitive.boneIndexes) != 0 {
		jData, wData := BuildSkinningAttributes(gltfMeshPrimitive.boneIndexes)

		jointsAcc := modeler.WriteJoints(a.doc, jData)
		weightsAcc := modeler.WriteWeights(a.doc, wData)

		attributes[gltf.JOINTS_0] = jointsAcc
		attributes[gltf.WEIGHTS_0] = weightsAcc
	}

	var material *int

	if len(gltfMeshPrimitive.uvCoords) != 0 && atlasIndex != nil {
		texCoordAccessor := modeler.WriteTextureCoord(a.doc, gltfMeshPrimitive.uvCoords)
		attributes[gltf.TEXCOORD_0] = texCoordAccessor

		materialIdx, ok := a.materialCache[*atlasIndex]
		if !ok {
			png := getAtlasPng(a.dav, *atlasIndex)
			imageIdx, err := modeler.WriteImage(a.doc, strconv.Itoa(int(*atlasIndex)), "image/png", bytes.NewReader(png))
			check(err)

			if a.samplerIndex == nil {
				a.doc.Samplers = append(a.doc.Samplers, &gltf.Sampler{
					MagFilter: gltf.MagNearest,
					MinFilter: gltf.MinNearest,
					WrapS:     gltf.WrapRepeat,
					WrapT:     gltf.WrapRepeat,
				})
				a.samplerIndex = gltf.Index(len(a.doc.Samplers) - 1)
			}

			a.doc.Textures = append(a.doc.Textures, &gltf.Texture{
				Source:  gltf.Index(imageIdx),
				Sampler: a.samplerIndex,
			})
			a.doc.Materials = append(a.doc.Materials, &gltf.Material{
				Name: "Texture",
				PBRMetallicRoughness: &gltf.PBRMetallicRoughness{
					BaseColorTexture: &gltf.TextureInfo{
						Index: len(a.doc.Textures) - 1,
					},
					MetallicFactor: gltf.Float(0),
				},

				// TODO: this is wrong, fix later based on polygon type pls
				// good luck adding additive blending support with gltf lmao
				AlphaMode:   gltf.AlphaMask,
				AlphaCutoff: gltf.Float(0.01),

				Extensions: gltf.Extensions{
					unlit.ExtensionName: unlit.Unlit{},
				},
			})

			materialIdx = len(a.doc.Materials) - 1
			a.materialCache[*atlasIndex] = materialIdx
		}

		material = gltf.Index(materialIdx)
	} else {
		if a.vertexColorMaterial == nil {
			a.doc.Materials = append(a.doc.Materials, &gltf.Material{
				Name: "Vertex Colors",
				PBRMetallicRoughness: &gltf.PBRMetallicRoughness{
					MetallicFactor: gltf.Float(0),
				},
				Extensions: gltf.Extensions{
					unlit.ExtensionName: unlit.Unlit{},
				},
			})

			a.vertexColorMaterial = gltf.Index(len(a.doc.Materials) - 1)
		}

		material = a.vertexColorMaterial
	}

	mesh.Primitives = append(mesh.Primitives, &gltf.Primitive{
		Indices:    gltf.Index(indicesAccessor),
		Attributes: attributes,
		Material:   material,
	})
}

type EulerAnimationTransform struct {
	translation [3]float32
	rotation    [3]float32
	scale       [3]float32
}

func decodeRot16(raw int16) float32 {
	val := float32(raw)

	rads := (val * math.Pi) / 2048.0

	if rads < 0 {
		rads += 2 * math.Pi
	}
	return rads
}

func negDecodeRot16(raw int16) float32 {
	val := float32(raw)

	rads := -(val * math.Pi) / 2048.0

	if rads < 0 {
		rads += 2 * math.Pi
	}
	return rads
}

func negDecodeRot8(raw int8) float32 {
	val := float32(raw)
	rads := -(val * math.Pi) / 64.0

	if rads < 0 {
		rads += 2 * math.Pi
	}
	return rads
}

func eulerToQuaternion32(pitch, yaw, roll float32) [4]float32 {
	q := eulerToQuaternion(pitch, yaw, roll)
	return [4]float32{
		float32(q[0]),
		float32(q[1]),
		float32(q[2]),
		float32(q[3]),
	}
}

func addBones(
	doc *gltf.Document,
	meshNode *gltf.Node,
	parentNode *gltf.Node,
	bones []WarSkeletonBone,
	animations []Animation,
) {
	initialNodeIdx := len(doc.Nodes)

	for i, bone := range bones {
		trsNode := &gltf.Node{
			Name: fmt.Sprintf("Bone_%d_TRS", i),
			Translation: [3]float64{
				float64(bone.X) / float64(WorldScale),
				-float64(bone.Y) / float64(WorldScale),
				-float64(bone.Z) / float64(WorldScale),
			},
			Scale: [3]float64{1.0, 1.0, 1.0},
		}
		scaleNode := &gltf.Node{
			Name:  fmt.Sprintf("Bone_%d_Joint", i),
			Scale: [3]float64{1.0, 1.0, 1.0},
		}
		if i == 0 {
			trsNode.Scale = [3]float64{1.0 / 8.0, 1.0 / 8.0, 1.0 / 8.0}
		}
		doc.Nodes = append(doc.Nodes, trsNode, scaleNode)

		trsNodeIdx := initialNodeIdx + (i * 2)
		scaleNodeIdx := initialNodeIdx + (i * 2) + 1
		doc.Nodes[trsNodeIdx].Children = append(doc.Nodes[trsNodeIdx].Children, scaleNodeIdx)
	}

	for i, bone := range bones {
		trsNodeIdx := initialNodeIdx + (i * 2)

		if i == 0 {
			if parentNode != nil {
				parentNode.Children = append(parentNode.Children, trsNodeIdx)
				continue
			}

			doc.Scenes[0].Nodes = append(doc.Scenes[0].Nodes, trsNodeIdx)
			continue
		}

		parentIndex := initialNodeIdx + (int(bone.ParentBoneIndex) * 2)
		doc.Nodes[parentIndex].Children = append(doc.Nodes[parentIndex].Children, trsNodeIdx)
	}

	if len(bones) != 0 {
		joints := make([]int, len(bones))
		for i := range bones {
			joints[i] = initialNodeIdx + (i * 2) + 1
		}

		doc.Skins = append(doc.Skins, &gltf.Skin{
			Joints:   joints,
			Skeleton: gltf.Index(initialNodeIdx),
		})

		meshNode.Skin = gltf.Index(len(doc.Skins) - 1)
	}

	for _, animation := range animations {
		times := make([]float32, len(animation.frames))
		var currentFrame uint32
		for i, frame := range animation.frames {
			times[i] = float32(currentFrame) / 1000
			currentFrame += uint32(frame.frameDuration)
		}

		lastTransforms := make(map[uint8]EulerAnimationTransform)
		transforms := make([]map[uint8]EulerAnimationTransform, len(animation.frames))
		for i := range transforms {
			transforms[i] = make(map[uint8]EulerAnimationTransform)
		}

		for i, frame := range animation.frames {
			buffer := bytes.NewReader(frame.transformData)
			for range frame.transformCount {
				var rawFlags uint16
				check(binary.Read(buffer, binary.LittleEndian, &rawFlags))

				flags := parseWarAnimationTransformFlags(rawFlags)
				boneNode := doc.Nodes[initialNodeIdx+(int(flags.BoneIndex)*2)]
				t := EulerAnimationTransform{
					translation: [3]float32{
						float32(boneNode.Translation[0]),
						float32(boneNode.Translation[1]),
						float32(boneNode.Translation[2]),
					},
					scale: [3]float32{1.0, 1.0, 1.0},
				}

				if !flags.Is8Bit {
					// 16bit values

					if flags.HasRotX {
						var v int16
						check(binary.Read(buffer, binary.LittleEndian, &v))
						t.rotation[0] += negDecodeRot16(v)
					}
					if flags.HasRotY {
						var v int16
						check(binary.Read(buffer, binary.LittleEndian, &v))
						t.rotation[1] -= negDecodeRot16(v)
					}
					if flags.HasRotZ {
						var v int16
						check(binary.Read(buffer, binary.LittleEndian, &v))
						t.rotation[2] -= negDecodeRot16(v)
					}

					if flags.HasPosX {
						var v int16
						check(binary.Read(buffer, binary.LittleEndian, &v))
						t.translation[0] += float32(v) / WorldScale
					}
					if flags.HasPosY {
						var v int16
						check(binary.Read(buffer, binary.LittleEndian, &v))
						t.translation[1] -= float32(v) / WorldScale
					}
					if flags.HasPosZ {
						var v int16
						check(binary.Read(buffer, binary.LittleEndian, &v))
						t.translation[2] -= float32(v) / WorldScale
					}

					if flags.HasSclX {
						var v uint16
						check(binary.Read(buffer, binary.LittleEndian, &v))
						t.scale[0] = float32(v) / 1024.0
					}
					if flags.HasSclY {
						var v uint16
						check(binary.Read(buffer, binary.LittleEndian, &v))
						t.scale[1] = float32(v) / 1024.0
					}
					if flags.HasSclZ {
						var v uint16
						check(binary.Read(buffer, binary.LittleEndian, &v))
						t.scale[2] = float32(v) / 1024.0
					}
				} else {
					// 8bit values

					if flags.HasRotX {
						var v int8
						check(binary.Read(buffer, binary.LittleEndian, &v))
						t.rotation[0] += negDecodeRot8(v)
					}
					if flags.HasRotY {
						var v int8
						check(binary.Read(buffer, binary.LittleEndian, &v))
						t.rotation[1] -= negDecodeRot8(v)
					}
					if flags.HasRotZ {
						var v int8
						check(binary.Read(buffer, binary.LittleEndian, &v))
						t.rotation[2] -= negDecodeRot8(v)
					}

					if flags.HasPosX {
						var v int8
						check(binary.Read(buffer, binary.LittleEndian, &v))
						t.translation[0] += float32(v) / WorldScale
					}
					if flags.HasPosY {
						var v int8
						check(binary.Read(buffer, binary.LittleEndian, &v))
						t.translation[1] -= float32(v) / WorldScale
					}
					if flags.HasPosZ {
						var v int8
						check(binary.Read(buffer, binary.LittleEndian, &v))
						t.translation[2] -= float32(v) / WorldScale
					}

					if flags.HasSclX {
						var v uint8
						check(binary.Read(buffer, binary.LittleEndian, &v))
						t.scale[0] = float32(v) / 128.0
					}
					if flags.HasSclY {
						var v uint8
						check(binary.Read(buffer, binary.LittleEndian, &v))
						t.scale[1] = float32(v) / 128.0
					}
					if flags.HasSclZ {
						var v uint8
						check(binary.Read(buffer, binary.LittleEndian, &v))
						t.scale[2] = float32(v) / 128.0
					}

					offset, err := buffer.Seek(0, io.SeekCurrent)
					check(err)

					if (offset % 2) != 0 {
						_, err = buffer.Seek(1, io.SeekCurrent)
						check(err)
					}
				}

				lastTransforms[flags.BoneIndex] = t
				transforms[i][flags.BoneIndex] = t
			}
		}

		gltfAnim := gltf.Animation{
			Name: animation.name,
		}
		timeAccessor := modeler.WriteAccessor(doc, gltf.TargetNone, times)
		doc.Accessors[timeAccessor].Min = []float64{float64(times[0])}
		doc.Accessors[timeAccessor].Max = []float64{float64(times[len(times)-1])}

		for _, boneIdx := range slices.Sorted(maps.Keys(lastTransforms)) {
			boneNode := doc.Nodes[initialNodeIdx+int(boneIdx)]
			t := EulerAnimationTransform{
				translation: [3]float32{
					float32(boneNode.Translation[0]),
					float32(boneNode.Translation[1]),
					float32(boneNode.Translation[2]),
				},
				scale: [3]float32{1.0, 1.0, 1.0},
			}

			translations := make([][3]float32, len(animation.frames))
			rotations := make([][4]float32, len(animation.frames))
			scales := make([][3]float32, len(animation.frames))
			for frameIdx, frame := range transforms {
				boneTransform, ok := frame[boneIdx]
				if ok {
					t = boneTransform
				}

				translations[frameIdx] = t.translation
				rotations[frameIdx] = eulerToQuaternion32(t.rotation[0], t.rotation[1], t.rotation[2])
				scales[frameIdx] = t.scale
			}

			translationAccessor := modeler.WriteAccessor(doc, gltf.TargetNone, translations)
			rotationAccessor := modeler.WriteAccessor(doc, gltf.TargetNone, rotations)
			scaleAccessor := modeler.WriteAccessor(doc, gltf.TargetNone, scales)

			// translations
			gltfAnim.Samplers = append(gltfAnim.Samplers, &gltf.AnimationSampler{
				Input:         timeAccessor,
				Output:        translationAccessor,
				Interpolation: gltf.InterpolationLinear,
			})
			gltfAnim.Channels = append(gltfAnim.Channels, &gltf.AnimationChannel{
				Sampler: len(gltfAnim.Samplers) - 1,
				Target: gltf.AnimationChannelTarget{
					Node: gltf.Index(initialNodeIdx + (int(boneIdx) * 2)),
					Path: gltf.TRSTranslation,
				},
			})

			// rotations
			gltfAnim.Samplers = append(gltfAnim.Samplers, &gltf.AnimationSampler{
				Input:         timeAccessor,
				Output:        rotationAccessor,
				Interpolation: gltf.InterpolationLinear,
			})
			gltfAnim.Channels = append(gltfAnim.Channels, &gltf.AnimationChannel{
				Sampler: len(gltfAnim.Samplers) - 1,
				Target: gltf.AnimationChannelTarget{
					Node: gltf.Index(initialNodeIdx + (int(boneIdx) * 2)),
					Path: gltf.TRSRotation,
				},
			})

			// scales
			gltfAnim.Samplers = append(gltfAnim.Samplers, &gltf.AnimationSampler{
				Input:         timeAccessor,
				Output:        scaleAccessor,
				Interpolation: gltf.InterpolationLinear,
			})
			gltfAnim.Channels = append(gltfAnim.Channels, &gltf.AnimationChannel{
				Sampler: len(gltfAnim.Samplers) - 1,
				Target: gltf.AnimationChannelTarget{
					Node: gltf.Index(initialNodeIdx + (int(boneIdx) * 2) + 1),
					Path: gltf.TRSScale,
				},
			})
		}

		doc.Animations = append(doc.Animations, &gltfAnim)
	}
}
