package main

import (
	"encoding/binary"
)

type WarColorTriangle struct {
	Indices [3]uint8
	Unused  uint8
	Color   WarColor
}

type WarColorQuad struct {
	Indices [4]uint8
	Color   WarColor
}

type WarColorBlendedTriangle struct {
	Indices [3]uint8
	Unused  uint8
	Colors  [3]WarColor
}

type WarColorBlendedQuad struct {
	Indices [4]uint8
	Colors  [4]WarColor
}

type WarTexturedTriangle struct {
	Indices       [3]uint8
	Unused        uint8
	UVs           [3]WarUV
	Unused2       uint16
	MaterialIndex uint32
	Color         WarColor
	Flags         uint32
}

type WarTexturedQuad struct {
	Indices       [4]uint8
	UVs           [4]WarUV
	MaterialIndex uint32
	Color         WarColor
	Unused        uint32
}

type WarColorBlendedTexturedTriangle struct {
	Indices       [3]uint8
	Unused        uint8
	UVs           [3]WarUV
	Unused2       uint16
	MaterialIndex uint32
	Colors        [3]WarColor
	Unused3       uint32
	Unused4       uint32
	Unused5       uint32
}

type WarColorBlendedTexturedQuad struct {
	Indices       [4]uint8
	UVs           [4]WarUV
	MaterialIndex uint32
	Colors        [4]WarColor
	Unused1       uint32
	Unused2       uint32
	Unused3       uint32
	Unused4       uint32
}

type WarColorBlendedTexturedQuadWithSquareUV struct {
	Indices       [4]uint8
	MaterialIndex uint32
	Unused1       uint32
	Unused2       uint32
	Colors        [4]WarColor
	Unused3       uint32
	Unused4       uint32
	Unused5       uint32
	Unused6       uint32
}

type WarTexturedQuadWithSquareUV struct {
	Indices       [4]uint8
	MaterialIndex uint32
	Unused1       uint32
	Unused2       uint32
	Color         WarColor
	Unused3       uint32
}

type WarColorBlendedTriangleWithAlphaMode struct {
	Indices [3]uint8
	Unused  uint8
	Colors  [3]WarColor
	Mode    uint32
}

type WarColorBlendedQuadWithAlphaMode struct {
	Indices [4]uint8
	Colors  [4]WarColor
	Mode    uint32
}

func (e *ModelExporter) setVertex(vertex Vertex, primitive *MeshPrimitive) uint16 {
	for existingIndex, existingVertex := range primitive.vertices {
		if vertex == existingVertex {
			return uint16(existingIndex)
		}
	}

	newIndex := uint16(len(primitive.vertices))
	primitive.vertices = append(primitive.vertices, vertex)
	return newIndex
}

func (e *ModelExporter) setColorVertex(vertexIndex uint8, color WarColor, primitive *MeshPrimitive) uint16 {
	index := uint16(vertexIndex)
	vertex := Vertex{
		position: e.positions[index],
		color:    [3]byte{color.Red, color.Green, color.Blue},
	}

	if e.isAnimated {
		vertex.boneIndex = e.boneIndexes[index]
	}

	return e.setVertex(vertex, primitive)
}

func (e *ModelExporter) setTextureVertex(
	vertexIndex uint8,
	color WarColor,
	materialIndex uint32,
	uv WarUV,
) (uint16, uint16) {
	index := uint16(vertexIndex)
	vertex := Vertex{
		position:      e.positions[index],
		color:         [3]byte{color.Red, color.Green, color.Blue},
		materialIndex: uint16(materialIndex),
		uv:            uv,
	}

	if e.isAnimated {
		vertex.boneIndex = e.boneIndexes[index]
	}

	var pageIndex uint16
	if e.dav != nil {
		textureInfo, ok := e.materialCache[vertex.materialIndex]
		if !ok {
			textureInfo = getMaterialInfo(e.dav, vertex.materialIndex)
			e.materialCache[vertex.materialIndex] = textureInfo
		}
		pageIndex = textureInfo.PageIndex
	}

	primitive := e.primitives.textured[pageIndex]
	newIndex := e.setVertex(vertex, &primitive)

	e.primitives.textured[pageIndex] = primitive

	return newIndex, pageIndex
}

func (e *ModelExporter) pushColorTriangle(indices [3]uint8, colors [3]WarColor) {
	p := &e.primitives.vertexColored
	i1 := e.setColorVertex(indices[0], colors[0], p)
	i2 := e.setColorVertex(indices[1], colors[1], p)
	i3 := e.setColorVertex(indices[2], colors[2], p)
	p.indices = append(p.indices, i1, i3, i2)
}

func (e *ModelExporter) pushColorQuad(indices [4]uint8, colors [4]WarColor) {
	p := &e.primitives.vertexColored
	i1 := e.setColorVertex(indices[0], colors[0], p)
	i2 := e.setColorVertex(indices[1], colors[1], p)
	i3 := e.setColorVertex(indices[2], colors[2], p)
	i4 := e.setColorVertex(indices[3], colors[3], p)
	p.indices = append(p.indices, i1, i3, i2, i2, i3, i4)
}

func (e *ModelExporter) pushTextureTriangle(indices [3]uint8, colors [3]WarColor, uv [3]WarUV, mIdx uint32) {
	i1, _ := e.setTextureVertex(indices[0], colors[0], mIdx, uv[0])
	i2, _ := e.setTextureVertex(indices[1], colors[1], mIdx, uv[1])
	i3, pageIndex := e.setTextureVertex(indices[2], colors[2], mIdx, uv[2])

	p := e.primitives.textured[pageIndex]
	p.indices = append(p.indices, i1, i3, i2)
	e.primitives.textured[pageIndex] = p
}

func (e *ModelExporter) pushTextureQuad(indices [4]uint8, colors [4]WarColor, uv [4]WarUV, mIdx uint32) {
	i1, _ := e.setTextureVertex(indices[0], colors[0], mIdx, uv[0])
	i2, _ := e.setTextureVertex(indices[1], colors[1], mIdx, uv[1])
	i3, _ := e.setTextureVertex(indices[2], colors[2], mIdx, uv[2])
	i4, pageIndex := e.setTextureVertex(indices[3], colors[3], mIdx, uv[3])

	p := e.primitives.textured[pageIndex]
	p.indices = append(p.indices, i1, i3, i2, i2, i3, i4)
	e.primitives.textured[pageIndex] = p
}

func (e *ModelExporter) handleColorTriangle() {
	p := WarColorTriangle{}
	check(binary.Read(e.war, binary.LittleEndian, &p))
	e.pushColorTriangle(
		p.Indices,
		[3]WarColor{p.Color, p.Color, p.Color},
	)
}

func (e *ModelExporter) handleColorQuad() {
	p := WarColorQuad{}
	check(binary.Read(e.war, binary.LittleEndian, &p))
	e.pushColorQuad(
		p.Indices,
		[4]WarColor{p.Color, p.Color, p.Color, p.Color},
	)
}

func (e *ModelExporter) handleColorBlendedTriangle() {
	p := WarColorBlendedTriangle{}
	check(binary.Read(e.war, binary.LittleEndian, &p))
	e.pushColorTriangle(
		p.Indices,
		p.Colors,
	)
}

func (e *ModelExporter) handleColorBlendedQuad() {
	p := WarColorBlendedQuad{}
	check(binary.Read(e.war, binary.LittleEndian, &p))
	e.pushColorQuad(
		p.Indices,
		p.Colors,
	)
}

func (e *ModelExporter) handleTexturedTriangle() {
	p := WarTexturedTriangle{}
	check(binary.Read(e.war, binary.LittleEndian, &p))
	e.pushTextureTriangle(
		p.Indices,
		[3]WarColor{p.Color, p.Color, p.Color},
		p.UVs,
		p.MaterialIndex,
	)
}

func (e *ModelExporter) handleTexturedQuad() {
	p := WarTexturedQuad{}
	check(binary.Read(e.war, binary.LittleEndian, &p))
	e.pushTextureQuad(
		p.Indices,
		[4]WarColor{p.Color, p.Color, p.Color, p.Color},
		p.UVs,
		p.MaterialIndex,
	)
}

func (e *ModelExporter) handleColorBlendedTexturedTriangle() {
	p := WarColorBlendedTexturedTriangle{}
	check(binary.Read(e.war, binary.LittleEndian, &p))
	e.pushTextureTriangle(
		p.Indices,
		p.Colors,
		p.UVs,
		p.MaterialIndex,
	)
}

func (e *ModelExporter) handleColorBlendedTexturedQuad() {
	p := WarColorBlendedTexturedQuad{}
	check(binary.Read(e.war, binary.LittleEndian, &p))
	e.pushTextureQuad(
		p.Indices,
		p.Colors,
		p.UVs,
		p.MaterialIndex,
	)
}

func (e *ModelExporter) handleColorBlendedTexturedQuadWithSquareUV(uvs [4]WarUV) {
	p := WarColorBlendedTexturedQuadWithSquareUV{}
	check(binary.Read(e.war, binary.LittleEndian, &p))
	e.pushTextureQuad(
		p.Indices,
		p.Colors,
		uvs,
		p.MaterialIndex,
	)
}

func (e *ModelExporter) handleTexturedQuadWithSquareUV(uvs [4]WarUV) {
	p := WarTexturedQuadWithSquareUV{}
	check(binary.Read(e.war, binary.LittleEndian, &p))
	e.pushTextureQuad(
		p.Indices,
		[4]WarColor{p.Color, p.Color, p.Color, p.Color},
		uvs,
		p.MaterialIndex,
	)
}

func (e *ModelExporter) handleColorBlendedTriangleWithAlphaMode() {
	p := WarColorBlendedTriangleWithAlphaMode{}
	check(binary.Read(e.war, binary.LittleEndian, &p))
	e.pushColorTriangle(
		p.Indices,
		p.Colors,
	)
}

func (e *ModelExporter) handleColorBlendedQuadWithAlphaMode() {
	p := WarColorBlendedQuadWithAlphaMode{}
	check(binary.Read(e.war, binary.LittleEndian, &p))
	e.pushColorQuad(
		p.Indices,
		p.Colors,
	)
}
