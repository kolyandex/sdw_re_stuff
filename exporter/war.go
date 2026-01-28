package main

import (
	"encoding/binary"
	"fmt"
	"io"
	"math"
	"os"
	"path/filepath"
	"slices"

	"github.com/qmuntal/gltf"
	"github.com/qmuntal/gltf/ext/unlit"
	"github.com/qmuntal/gltf/modeler"
)

const WorldScale float32 = 100.0

type RgbaModel struct {
	positions [][3]float32
	indices   []uint16
	colors    [][4]float32
}

func exportModelToGltf(
	war *os.File,
	dav *os.File,
	resourceOffset uint32,
	modelType uint8,
	outPath string,
) {
	if !slices.Contains([]uint8{3, 4, 11, 38}, modelType) {
		panic("resource is not a model")
	}

	modelExporter := ModelExporter{
		war: war,
		dav: dav,
	}

	model := modelExporter.getModel(resourceOffset, modelType)

	doc := gltf.NewDocument()

	doc.ExtensionsUsed = append(doc.ExtensionsUsed, unlit.ExtensionName)

	doc.Meshes = []*gltf.Mesh{{
		Name: "Mesh",
	}}
	doc.Nodes = []*gltf.Node{{Name: "Root", Mesh: gltf.Index(0)}}
	doc.Scenes[0].Nodes = append(doc.Scenes[0].Nodes, 0)

	addBones(doc, doc.Nodes[0], nil, model.bones, model.animations)

	gltfPrimitiveAdder := GltfPrimitiveAdder{
		doc:           doc,
		dav:           dav,
		materialCache: make(map[uint16]int),
	}

	gltfPrimitiveAdder.addGltfMeshPrimitive(doc.Meshes[0], model.vertexColored, nil)
	for atlasIndex, primitive := range model.textured {
		gltfPrimitiveAdder.addGltfMeshPrimitive(doc.Meshes[0], primitive, &atlasIndex)
	}

	check(gltf.SaveBinary(doc, outPath))
}

func getResources(war *os.File) []uint32 {
	header := WarHeader{}
	check(binary.Read(war, binary.LittleEndian, &header))

	resources := make([]uint32, header.ResourceCount)
	check(binary.Read(war, binary.LittleEndian, &resources))

	return resources
}

func modelExport(warPath string, davPath string, outPath string, modelIdx uint32) {
	war, err := os.Open(warPath)
	check(err)
	defer war.Close()

	var dav *os.File
	if davPath != "" {
		dav, err = os.Open(davPath)
		check(err)
	}
	defer dav.Close()

	resources := getResources(war)

	if int(modelIdx) >= len(resources) {
		panic("model index too high")
	}

	resource := resources[modelIdx]
	resourceOffset := resource & 0xFFFFFF

	resourceType := uint8(resource >> 24)
	modelType := resourceType & 0xBF

	if modelType == 4 {
		fmt.Println("this is an animated model!")
	}

	exportModelToGltf(
		war,
		dav,
		resourceOffset,
		modelType,
		outPath,
	)
}

func modelExportToFolder(warPath string, davPath string, outPath string) {
	war, err := os.Open(warPath)
	check(err)
	defer war.Close()

	var dav *os.File
	if davPath != "" {
		dav, err = os.Open(davPath)
		check(err)
	}
	defer dav.Close()

	resources := getResources(war)

	check(os.MkdirAll(outPath, 0755))

	fmt.Print("exporting")

	for i, resource := range resources {
		resourceType := uint8(resource >> 24)
		modelType := resourceType & 0xBF

		if !slices.Contains([]uint8{3, 4, 11}, modelType) {
			continue
		}

		// skip if level geometry
		if (resourceType & 0x40) == 0 {
			continue
		}

		resourceOffset := resource & 0xFFFFFF
		isAnimated := modelType == 4

		glbPath := outPath

		if isAnimated {
			glbPath = filepath.Join(glbPath, "animated")
			check(os.MkdirAll(glbPath, 0755))
		}

		glbPath = filepath.Join(glbPath, fmt.Sprintf("%d.glb", i))

		exportModelToGltf(
			war,
			dav,
			resourceOffset,
			modelType,
			glbPath,
		)

		fmt.Print(".")
	}

	fmt.Println("done!")
}

func skyboxExport(warPath string, davPath string, outPath string) {
	war, err := os.Open(warPath)
	check(err)
	defer war.Close()

	var dav *os.File
	if davPath != "" {
		dav, err = os.Open(davPath)
		check(err)
	}
	defer dav.Close()

	modelExporter := ModelExporter{
		war: war,
		dav: dav,
	}

	doc := gltf.NewDocument()
	doc.ExtensionsUsed = append(doc.ExtensionsUsed, unlit.ExtensionName)

	gltfPrimitiveAdder := GltfPrimitiveAdder{
		doc:           doc,
		dav:           dav,
		materialCache: make(map[uint16]int),
	}

	skyboxMesh := gltf.Mesh{
		Name: "Mesh",
	}

	resources := getResources(war)

	for _, resource := range resources {
		resourceType := uint8(resource >> 24)
		modelType := resourceType & 0xBF

		// skybox check
		if modelType != 38 {
			continue
		}

		resourceOffset := resource & 0xFFFFFF

		model := modelExporter.getModel(resourceOffset, modelType)
		gltfPrimitiveAdder.addGltfMeshPrimitive(&skyboxMesh, model.vertexColored, nil)
		for atlasIndex, primitive := range model.textured {
			gltfPrimitiveAdder.addGltfMeshPrimitive(&skyboxMesh, primitive, &atlasIndex)
		}
	}

	if len(skyboxMesh.Primitives) == 0 {
		panic("skybox not found")
	}

	doc.Meshes = []*gltf.Mesh{&skyboxMesh}
	doc.Nodes = []*gltf.Node{{Name: "Skybox", Mesh: gltf.Index(0)}}
	doc.Scenes[0].Nodes = append(doc.Scenes[0].Nodes, 0)

	check(gltf.SaveBinary(doc, outPath))
}

type CorrectedBox struct {
	X1 float32
	Y1 float32
	Z1 float32
	X2 float32
	Y2 float32
	Z2 float32
}

type CachedMesh struct {
	meshIdx int
	bones   []WarSkeletonBone
}

type LevelExporter struct {
	war     *os.File
	exports map[uint16][]uint32

	collisionMapOffset     uint32
	collisionPolygonOffset uint32

	sam               SamClass
	checkpointManager CheckpointManagerClass

	meshCache map[uint16]CachedMesh
}

func (e *LevelExporter) handleSam(scenaricResource WarScenaricResource) {
	switch scenaricResource.ClassID {
	case 1:
		check(binary.Read(e.war, binary.LittleEndian, &e.sam))
	case 25:
		check(binary.Read(e.war, binary.LittleEndian, &e.checkpointManager))
	}
}

func (e *LevelExporter) getExports(exportSectionOffset uint32) {
	_, err := e.war.Seek(int64(exportSectionOffset), io.SeekStart)
	check(err)

	var exportCount uint32
	check(binary.Read(e.war, binary.LittleEndian, &exportCount))

	e.exports = make(map[uint16][]uint32, exportCount)

	for range exportCount {
		exportHeader := WarExportHeader{}
		check(binary.Read(e.war, binary.LittleEndian, &exportHeader))

		offsets := make([]uint32, exportHeader.Count)
		check(binary.Read(e.war, binary.LittleEndian, &offsets))

		e.exports[exportHeader.Id] = offsets
	}
}

func (e *LevelExporter) getSamBoxes(exportId uint16, color [4]uint8) []RgbaModel {
	offsets, ok := e.exports[exportId]
	if !ok {
		return []RgbaModel{}
	}

	models := make([]RgbaModel, len(offsets))

	for i, offset := range offsets {
		_, err := e.war.Seek(int64(offset), io.SeekStart)
		check(err)

		var flags uint32
		check(binary.Read(e.war, binary.LittleEndian, &flags))

		//if (flags & 1) != 0 {
		//	continue
		//}

		warBox := WarBox{}
		check(binary.Read(e.war, binary.LittleEndian, &warBox))

		box := CorrectedBox{
			X1: float32(warBox.X1) / WorldScale,
			Y1: -float32(warBox.Y1) / WorldScale,
			Z1: -float32(warBox.Z1) / WorldScale,
			X2: float32(warBox.X2) / WorldScale,
			Y2: -float32(warBox.Y2) / WorldScale,
			Z2: -float32(warBox.Z2) / WorldScale,
		}

		model := RgbaModel{
			positions: [][3]float32{
				// Bottom face
				{box.X1, box.Y1, box.Z1}, // 0
				{box.X2, box.Y1, box.Z1}, // 1
				{box.X2, box.Y1, box.Z2}, // 2
				{box.X1, box.Y1, box.Z2}, // 3

				// Top face
				{box.X1, box.Y2, box.Z1}, // 4
				{box.X2, box.Y2, box.Z1}, // 5
				{box.X2, box.Y2, box.Z2}, // 6
				{box.X1, box.Y2, box.Z2}, // 7
			},
			indices: []uint16{
				// Bottom face
				0, 1, 2,
				0, 2, 3,

				// Top face
				4, 5, 6,
				4, 6, 7,

				// Front face
				0, 1, 5,
				0, 5, 4,

				// Back face
				3, 2, 6,
				3, 6, 7,

				// Left face
				0, 3, 7,
				0, 7, 4,

				// Right face
				1, 2, 6,
				1, 6, 5,
			},
		}

		linearColor := gltf.DenormalizeRGBA(color)

		model.colors = make([][4]float32, len(model.positions))
		for j := range model.colors {
			model.colors[j] = linearColor
		}

		models[i] = model
	}

	return models
}

func (e *LevelExporter) getCollisionModel() GltfMeshPrimitive {
	_, err := e.war.Seek(int64(e.collisionMapOffset), io.SeekStart)
	check(err)

	mapHeader := WarCollisionMapHeader{}
	check(binary.Read(e.war, binary.LittleEndian, &mapHeader))

	allPolygonIndexes := make(map[uint32]bool)

	for range mapHeader.GridHeight {
		for range mapHeader.GridWidth {
			var polygonCount uint32
			check(binary.Read(e.war, binary.LittleEndian, &polygonCount))

			polygonIndexes := make([]uint32, polygonCount)
			check(binary.Read(e.war, binary.LittleEndian, &polygonIndexes))

			for _, index := range polygonIndexes {
				allPolygonIndexes[index] = true
			}
		}
	}

	var positions [][3]float32
	indices := make([]uint16, len(allPolygonIndexes)*3)
	currentIndicesIndex := 0
	for index := range allPolygonIndexes {
		_, err := e.war.Seek(int64(e.collisionPolygonOffset)+(int64(index)*40), io.SeekStart)
		check(err)

		polygon := WarCollisionPolygon{}
		check(binary.Read(e.war, binary.LittleEndian, &polygon))

		for _, vertex := range polygon.Vertices {
			position := [3]float32{
				float32(vertex.X) / WorldScale,
				float32(vertex.Y) / WorldScale,
				float32(vertex.Z) / WorldScale,
			}

			existingIndex := slices.Index(positions, position)
			if existingIndex == -1 {
				indices[currentIndicesIndex] = uint16(len(positions))
				positions = append(positions, position)
			} else {
				indices[currentIndicesIndex] = uint16(existingIndex)
			}

			currentIndicesIndex++
		}
	}

	return GltfMeshPrimitive{
		positions: positions,
		indices:   indices,
	}
}

func eulerToQuaternion(pitch, yaw, roll float32) [4]float64 {
	p := float64(pitch)
	y := float64(yaw)
	r := float64(roll)

	cp, sp := math.Cos(p/2), math.Sin(p/2)
	cy, sy := math.Cos(y/2), math.Sin(y/2)
	cr, sr := math.Cos(r/2), math.Sin(r/2)

	qx := sp*cy*cr + cp*sy*sr
	qy := cp*sy*cr - sp*cy*sr
	qz := cp*cy*sr - sp*sy*cr
	qw := cp*cy*cr + sp*sy*sr

	return [4]float64{qx, qy, qz, qw}
}

type TriggerInfo struct {
	name     string
	exportId uint32
	color    [4]uint8
}

func levelExport(warPath string, davPath string, outPath string) {
	war, err := os.Open(warPath)
	check(err)
	defer war.Close()

	var dav *os.File
	if davPath != "" {
		dav, err = os.Open(davPath)
		check(err)
	}
	defer dav.Close()

	resources := getResources(war)

	levelMesh := gltf.Mesh{
		Name:       "Mesh",
		Primitives: []*gltf.Primitive{},
	}

	levelExporter := LevelExporter{
		war:       war,
		meshCache: make(map[uint16]CachedMesh),
	}

	doc := gltf.NewDocument()

	doc.ExtensionsUsed = []string{
		unlit.ExtensionName,
		"KHR_node_visibility",
	}

	gltfPrimitiveAdder := GltfPrimitiveAdder{
		doc:           doc,
		dav:           dav,
		materialCache: make(map[uint16]int),
	}

	modelExporter := ModelExporter{
		war: war,
		dav: dav,
	}

	actorContainerNode := gltf.Node{
		Name: "Actors",
	}

	skinnedMeshContainerNode := gltf.Node{
		Name: "Skinned Meshes",
	}

	for _, resource := range resources {
		resourceType := uint8(resource >> 24)
		resourceOffset := resource & 0xFFFFFF

		if resourceType == 5 {
			_, err := war.Seek(int64(resourceOffset), io.SeekStart)
			check(err)

			scenaricResource := WarScenaricResource{}
			check(binary.Read(war, binary.LittleEndian, &scenaricResource))

			levelExporter.handleSam(scenaricResource)

			modelResource := resources[scenaricResource.ModelResourceIndex]
			modelResourceType := uint8(modelResource >> 24)
			modelResourceOffset := modelResource & 0xFFFFFF

			cachedMesh, ok := levelExporter.meshCache[scenaricResource.ModelResourceIndex]
			meshIdx := cachedMesh.meshIdx
			bones := cachedMesh.bones
			if !ok {
				model := modelExporter.getModel(modelResourceOffset, modelResourceType&0xBF)

				mesh := gltf.Mesh{
					Name:       "Mesh",
					Primitives: []*gltf.Primitive{},
				}

				gltfPrimitiveAdder.addGltfMeshPrimitive(&mesh, model.vertexColored, nil)
				for atlasIndex, primitive := range model.textured {
					gltfPrimitiveAdder.addGltfMeshPrimitive(&mesh, primitive, &atlasIndex)
				}

				meshIdx = len(doc.Meshes)
				doc.Meshes = append(doc.Meshes, &mesh)

				bones = model.bones

				levelExporter.meshCache[scenaricResource.ModelResourceIndex] = CachedMesh{
					meshIdx: meshIdx,
					bones:   bones,
				}
			}

			// skinned model
			if len(bones) > 0 {
				meshNodeIdx := len(doc.Nodes)
				meshNode := &gltf.Node{
					Name:     ScenaricClassMap[scenaricResource.ClassID],
					Mesh:     gltf.Index(meshIdx),
					Rotation: [4]float64{0, 0, 0, 1},
					Scale:    [3]float64{1, 1, 1},
				}
				doc.Nodes = append(doc.Nodes, meshNode)
				skinnedMeshContainerNode.Children = append(skinnedMeshContainerNode.Children, meshNodeIdx)

				transformNodeIdx := len(doc.Nodes)
				transformNode := &gltf.Node{
					Name: ScenaricClassMap[scenaricResource.ClassID],
					Translation: [3]float64{
						float64(scenaricResource.X) / float64(WorldScale),
						-float64(scenaricResource.Y) / float64(WorldScale),
						-float64(scenaricResource.Z) / float64(WorldScale),
					},
					Rotation: eulerToQuaternion(
						decodeRot16(scenaricResource.Pitch),
						decodeRot16(-scenaricResource.Yaw),
						decodeRot16(-scenaricResource.Roll),
					),
					//Scale:       [3]float64{1.0 / 8.0, 1.0 / 8.0, 1.0 / 8.0},
				}
				doc.Nodes = append(doc.Nodes, transformNode)
				actorContainerNode.Children = append(actorContainerNode.Children, transformNodeIdx)

				addBones(doc, doc.Nodes[meshNodeIdx], doc.Nodes[transformNodeIdx], bones, []Animation{})

				continue
			}

			// non-skinned model
			nodeIdx := len(doc.Nodes)
			doc.Nodes = append(doc.Nodes, &gltf.Node{
				Name: ScenaricClassMap[scenaricResource.ClassID],
				Mesh: gltf.Index(meshIdx),
				Translation: [3]float64{
					float64(scenaricResource.X) / float64(WorldScale),
					-float64(scenaricResource.Y) / float64(WorldScale),
					-float64(scenaricResource.Z) / float64(WorldScale),
				},
				Rotation: eulerToQuaternion(
					decodeRot16(scenaricResource.Pitch),
					decodeRot16(-scenaricResource.Yaw),
					decodeRot16(-scenaricResource.Roll),
				),
			})
			actorContainerNode.Children = append(actorContainerNode.Children, nodeIdx)

			continue
		}

		if resourceType == 0x81 {
			levelExporter.collisionPolygonOffset = resourceOffset
			continue
		}

		if resourceType == 0x80 {
			levelExporter.collisionMapOffset = resourceOffset
			collisionModel := levelExporter.getCollisionModel()

			positionAccessor := modeler.WritePosition(doc, collisionModel.positions)
			indicesAccessor := modeler.WriteIndices(doc, collisionModel.indices)

			collisionMesh := gltf.Mesh{
				Name: "Mesh",
				Primitives: []*gltf.Primitive{{
					Indices: gltf.Index(indicesAccessor),
					Attributes: gltf.PrimitiveAttributes{
						gltf.POSITION: positionAccessor,
					},
				}},
			}

			meshIdx := len(doc.Meshes)
			doc.Meshes = append(doc.Meshes, &collisionMesh)

			nodeIdx := len(doc.Nodes)
			doc.Nodes = append(doc.Nodes, &gltf.Node{
				Name: "Level Collision",
				Mesh: gltf.Index(meshIdx),
				Extensions: gltf.Extensions{
					"KHR_node_visibility": map[string]bool{
						"visible": false,
					},
				},
			})
			doc.Scenes[0].Nodes = append(doc.Scenes[0].Nodes, nodeIdx)

			continue
		}

		if resourceType == 0x82 {
			levelExporter.getExports(resourceOffset)

			triggers := []TriggerInfo{
				{
					name:     "Sam Orange Zone",
					exportId: levelExporter.sam.ORANGEZONEBOX,
					color:    [4]uint8{252, 132, 3, 64},
				},
				{
					name:     "Sam Green Zone",
					exportId: levelExporter.sam.GREENZONEBOX,
					color:    [4]uint8{0, 255, 0, 64},
				},
				{
					name:     "Sam Authorized Zone",
					exportId: levelExporter.sam.AUTHORIZEDZONE,
					color:    [4]uint8{255, 255, 255, 64},
				},
				{
					name:     "Sam \"Wolf can be hit\" Zone",
					exportId: levelExporter.sam.WOLFCANBEHITZONE,
					color:    [4]uint8{255, 0, 0, 64},
				},
			}

			for i, checkpoint := range levelExporter.checkpointManager.Checks {
				triggers = append(triggers,
					TriggerInfo{
						name:     fmt.Sprintf("Checkpoint %d", i+1),
						exportId: checkpoint.BOXES,
						color:    [4]uint8{0, 0, 255, 64},
					},
					TriggerInfo{
						name:     fmt.Sprintf("Checkpoint %d Sheep Spawnpoint", i+1),
						exportId: checkpoint.SHEEP,
						color:    [4]uint8{0, 255, 255, 64},
					},
					TriggerInfo{
						name:     fmt.Sprintf("Checkpoint %d Wolf Spawnpoint", i+1),
						exportId: checkpoint.WOLF,
						color:    [4]uint8{255, 0, 255, 64},
					},
				)
			}

			materialIdx := len(doc.Materials)
			doc.Materials = append(doc.Materials, &gltf.Material{
				PBRMetallicRoughness: &gltf.PBRMetallicRoughness{
					BaseColorFactor: &[4]float64{1, 1, 1, 1},
				},
				AlphaMode:   gltf.AlphaBlend,
				DoubleSided: true,
			})

			boxContainerNode := gltf.Node{
				Name: "Boxes",
				Extensions: gltf.Extensions{
					"KHR_node_visibility": map[string]bool{
						"visible": false,
					},
				},
			}

			for _, trigger := range triggers {
				boxes := levelExporter.getSamBoxes(uint16(trigger.exportId), trigger.color)

				if len(boxes) == 0 {
					continue
				}

				mesh := gltf.Mesh{
					Name:       "Mesh",
					Primitives: []*gltf.Primitive{},
				}

				for _, box := range boxes {
					positionAccessor := modeler.WritePosition(doc, box.positions)
					indicesAccessor := modeler.WriteIndices(doc, box.indices)
					colorIndices := modeler.WriteColor(doc, box.colors)

					mesh.Primitives = append(mesh.Primitives,
						&gltf.Primitive{
							Indices: gltf.Index(indicesAccessor),
							Attributes: gltf.PrimitiveAttributes{
								gltf.POSITION: positionAccessor,
								gltf.COLOR_0:  colorIndices,
							},
							Material: gltf.Index(materialIdx),
						},
					)
				}

				meshIdx := len(doc.Meshes)
				doc.Meshes = append(doc.Meshes, &mesh)

				nodeIdx := len(doc.Nodes)
				doc.Nodes = append(doc.Nodes, &gltf.Node{Name: trigger.name, Mesh: gltf.Index(meshIdx)})
				boxContainerNode.Children = append(boxContainerNode.Children, nodeIdx)
			}

			if len(boxContainerNode.Children) != 0 {
				nodeIdx := len(doc.Nodes)
				doc.Nodes = append(doc.Nodes, &boxContainerNode)
				doc.Scenes[0].Nodes = append(doc.Scenes[0].Nodes, nodeIdx)
			}

			continue
		}

		// skip if not level geometry
		if ((resourceType & 0x40) != 0) || !slices.Contains([]uint8{3, 11}, resourceType&0xBF) {
			continue
		}

		model := modelExporter.getModel(resourceOffset, resourceType&0xBF)

		gltfPrimitiveAdder.addGltfMeshPrimitive(&levelMesh, model.vertexColored, nil)
		for atlasIndex, primitive := range model.textured {
			gltfPrimitiveAdder.addGltfMeshPrimitive(&levelMesh, primitive, &atlasIndex)
		}
	}

	meshIdx := len(doc.Meshes)
	doc.Meshes = append(doc.Meshes, &levelMesh)

	nodeIdx := len(doc.Nodes)
	doc.Nodes = append(doc.Nodes, &gltf.Node{Name: "Level", Mesh: gltf.Index(meshIdx)})
	doc.Scenes[0].Nodes = append(doc.Scenes[0].Nodes, nodeIdx)

	if len(actorContainerNode.Children) != 0 {
		nodeIdx := len(doc.Nodes)
		doc.Nodes = append(doc.Nodes, &actorContainerNode)
		doc.Scenes[0].Nodes = append(doc.Scenes[0].Nodes, nodeIdx)
	}

	if len(skinnedMeshContainerNode.Children) != 0 {
		nodeIdx := len(doc.Nodes)
		doc.Nodes = append(doc.Nodes, &skinnedMeshContainerNode)
		doc.Scenes[0].Nodes = append(doc.Scenes[0].Nodes, nodeIdx)
	}

	fmt.Println("done")

	check(gltf.SaveBinary(doc, outPath))
}
