#pragma endian little
#pragma pattern_limit 0x200000

import std.io;
import type.base;

struct Vertex {
    s16 x, y, z, unused;
};

struct ColorTriangle {
    u8 index1;
    u8 index2;
    u8 index3;
    u8 pad;
    u32 color;
};

struct ColorQuad {
    u8 index1;
    u8 index2;
    u8 index3;
    u8 index4;
    u32 color;
};

struct ColorBlendedTriangle {
    u8 index1;
    u8 index2;
    u8 index3;
    u8 pad;
    u32 color1;
    u32 color2;
    u32 color3;
};

struct ColorBlendedQuad {
    u8 index1;
    u8 index2;
    u8 index3;
    u8 index4;
    u32 color1;
    u32 color2;
    u32 color3;
    u32 color4;
};

struct TexturedTriangle {
    u8 index1;
    u8 index2;
    u8 index3;
    u8 pad;
    u8 UV1_u;
    u8 UV1_v;
    u8 UV2_u;
    u8 UV2_v;
    u8 UV3_u;
    u8 UV3_v;
    u16 unused;
    u32 materialIdx;
    u32 color;
    u32 flags_unused_maybe;
};

struct TexturedQuad {
    u8 index1;
    u8 index2;
    u8 index3;
    u8 index4;
    u8 UV1_u;
    u8 UV1_v;
    u8 UV2_u;
    u8 UV2_v;
    u8 UV3_u;
    u8 UV3_v;
    u8 UV4_u;
    u8 UV4_v;
    u32 materialIdx;
    u32 color;
    u32 unused;
};

struct ColorBlendedTexturedTriangle {
    u8 index1;
    u8 index2;
    u8 index3;
    u8 pad;
    u8 UV1_u;
    u8 UV1_v;
    u8 UV2_u;
    u8 UV2_v;
    u8 UV3_u;
    u8 UV3_v;
    u16 unused;
    u32 materialIdx;
    u32 color1;
    u32 color2;
    u32 color3;
    u32 unused1;
    u32 unused2;
    u32 unused3;
};

struct ColorBlendedTexturedQuad {
    u8 index1;
    u8 index2;
    u8 index3;
    u8 index4;
    u8 UV1_u;
    u8 UV1_v;
    u8 UV2_u;
    u8 UV2_v;
    u8 UV3_u;
    u8 UV3_v;
    u8 UV4_u;
    u8 UV4_v;
    u32 materialIdx;
    u32 color1;
    u32 color2;
    u32 color3;
    u32 color4;
    u32 unused1;
    u32 unused2;
    u32 unused3;
    u32 unused4;
};

struct ColorBlendedTexturedQuadWithSquareUV {
    u8 index1;
    u8 index2;
    u8 index3;
    u8 index4;
    u32 materialIdx;
    u32 unused1;
    u32 unused2;
    u32 color1;
    u32 color2;
    u32 color3;
    u32 color4;
    u32 unused3;
    u32 unused4;
    u32 unused5;
    u32 unused6;
};

struct TexturedQuadWithSquareUV {
    u8 index1;
    u8 index2;
    u8 index3;
    u8 index4;
    u32 materialIdx;
    u32 unused1;
    u32 unused2;
    u32 color;
    u32 unused3;
};

struct ColorBlendedTriangleWithAlphaMode {
    u8 index1;
    u8 index2;
    u8 index3;
    u8 pad;
    u32 color1;
    u32 color2;
    u32 color3;
    u32 mode; //  can be 0, 1, 2, 3
};

struct ColorBlendedQuadWithAlphaMode {
    u8 index1;
    u8 index2;
    u8 index3;
    u8 index4;
    u32 color1;
    u32 color2;
    u32 color3;
    u32 color4;
    u32 mode; //  can be 0, 1, 2, 3
};

struct MeshChunk {
    s16 type;
    u16 count;

    match (type) {
        (0x0): ColorTriangle polygons[count];
        (0x1): ColorQuad polygons[count];
        (0x2): ColorBlendedTriangle polygons[count];
        (0x3): ColorBlendedQuad polygons[count];
        (0x4 | 0x14): TexturedTriangle polygons[count];
        (0x5): TexturedQuad polygons[count];
        (0x6 | 0x15): ColorBlendedTexturedTriangle polygons[count];
        (0x7): ColorBlendedTexturedQuad polygons[count];

        // uv scale values:
        // 0x8, 0x9: 64
        // 0xA, 0xB: 32
        (0x8 | 0x9 | 0xa | 0xb): ColorBlendedTexturedQuadWithSquareUV polygons[count];
        (0xc | 0xd | 0xe | 0xf): TexturedQuadWithSquareUV polygons[count];

        (0x12 | 0x16): ColorBlendedTriangleWithAlphaMode polygons[count];
        (0x13): ColorBlendedQuadWithAlphaMode polygons[count];
        (_): {
            std::print("unknown polygon type {}", type);
            break;
        }
    }
};

struct Box {
    u32 flags; // inactive if bit 0 is 1

    if ((flags & 1) == 0) {
        s16 minX, minY, minZ;
        s16 maxX, maxY, maxZ;
    }
};

struct BoxContainer {
    u32 count;
    Box boxes[count];
};

struct Model {
    u32 pointer_vert;
    u32 pointer_mesh;
    u16 vert_size;
    u16 mesh_chunk_size;
    u32 pointer_box; // optional, none if 0

    Vertex verts[vert_size] @ pointer_vert;
    MeshChunk mesh[mesh_chunk_size] @ pointer_mesh;
    if (pointer_box != 0) {
        BoxContainer box @ pointer_box;
    }
};

struct SkeletonBone {
    u16 parentIndex;
    s16 x, y, z;
    u16 vertCount;
};

bitfield AnimationTransformFlags {
    bone_index : 6;
    has_rot_x  : 1;
    has_rot_y  : 1;
    has_rot_z  : 1;
    has_pos_x  : 1;
    has_pos_y  : 1;
    has_pos_z  : 1;
    has_scl_x  : 1;
    has_scl_y  : 1;
    has_scl_z  : 1;
    is_8bit    : 1;
};

struct AnimationTransform {
    AnimationTransformFlags flags;

    if (flags.is_8bit == 0) {
        // Rotations: u16 -> Radians mapped -PI to PI
        if (flags.has_rot_x) s16 rot_x;
        if (flags.has_rot_y) s16 rot_y;
        if (flags.has_rot_z) s16 rot_z;

        // Translations: s16 -> Cast to Float directly
        if (flags.has_pos_x) s16 pos_x;
        if (flags.has_pos_y) s16 pos_y;
        if (flags.has_pos_z) s16 pos_z;

        // Scales: u16 -> Fixed point (val / 1024.0)
        if (flags.has_scl_x) u16 scl_x;
        if (flags.has_scl_y) u16 scl_y;
        if (flags.has_scl_z) u16 scl_z;
    } else {
        // Rotations: u8 -> Radians
        if (flags.has_rot_x) s8 rot_x_8;
        if (flags.has_rot_y) s8 rot_y_8;
        if (flags.has_rot_z) s8 rot_z_8;

        // Translations: s8 -> Float
        if (flags.has_pos_x) s8 pos_x_8;
        if (flags.has_pos_y) s8 pos_y_8;
        if (flags.has_pos_z) s8 pos_z_8;

        // Scales: u8 -> Fixed point (val / 128.0)
        if (flags.has_scl_x) u8 scl_x_8;
        if (flags.has_scl_y) u8 scl_y_8;
        if (flags.has_scl_z) u8 scl_z_8;

        // 16-bit alignment
        if (($ % 2) != 0) {
            padding[1];
        }
    }
};

struct AnimationKeyframe {
    u16 duration_milliseconds;
    u16 num_transforms;
    u16 data_size_shorts;
    u16 sound_id; // none if 0

    //u32 frame_end_addr = $ + (data_size_shorts * 2);

    AnimationTransform anim_transforms[num_transforms];
};

struct Animation {
    char name[8];
    u16 keyframe_count;

    AnimationKeyframe keyframes[keyframe_count];
};

struct AnimationOffset {
    u32 offset;
    if (offset != 0) {
        Animation animation @ offset;
    }
};

struct AnimationList {
    u32 anims_1_count;
    AnimationOffset anims_1_offsets[anims_1_count];

    // seems to have less anims than the first array?
    u32 anims_2_count;
    /* AnimationOffset */ u32 anims_2_offsets[anims_2_count];
};

struct AnimatedModel : Model {
    u32 pointer_animlist;
    u32 pointer_skeleton;
    u16 boneCount;
    u16 pad;

    SkeletonBone skeleton[boneCount] @ pointer_skeleton;

    AnimationList animationList @ pointer_animlist;
};

struct CinExp {
    u16 unk1;
    u16 unk2;
    u32 offset;
};

struct CinExpHeader {
    u32 size;
    CinExp cinexp[size];
};

struct SamClass {
    u32 PROPERTY_SAM_2NDCAMERABOX;
    u32 PROPERTY_SAM_AUTHORIZEDZONE;
    u32 PROPERTY_SAM_CATCHABLESHEEPBOX;
    u32 PROPERTY_SAM_CIN01;
    u32 PROPERTY_SAM_CIN01BOX;
    u32 PROPERTY_SAM_CIN01FLAGS;
    u32 PROPERTY_SAM_CIN01SHEEPBOX;
    u32 PROPERTY_SAM_CIN01TEXT;
    u32 PROPERTY_SAM_DEATHDURATIONMS;
    u32 PROPERTY_SAM_DISTCATCH_PATROL;
    u32 PROPERTY_SAM_DNTTRYTOCATCHSHPBOX;
    u32 PROPERTY_SAM_GREENZONEBOX;
    u32 PROPERTY_SAM_HEADSPEED;
    u32 PROPERTY_SAM_HIDINGBOXES;
    u32 PROPERTY_SAM_HYPNOTIZEDZONE;
    u32 PROPERTY_SAM_MAXANGLE;
    u32 PROPERTY_SAM_MODE;
    u32 PROPERTY_SAM_ORANGEZONEBOX;
    u32 PROPERTY_SAM_PUTSHEEPBOX;
    u32 PROPERTY_SAM_TRAJECTORY;
    u32 PROPERTY_SAM_WATCHROBOTTIMEMS;
    u32 PROPERTY_SAM_WOLFCANBEHITZONE;
};

struct TimeMachineSphereObject {
    u32 PAST;
    u32 PRESENT;
};

struct TimeMachineSphereClass {
    u32 BOXPAST;
    u32 BOXPRESENT;
    TimeMachineSphereObject objects[16];
};

struct VisibilityManagerClass {
    u32 CANTSEE[3];
    u32 WHENINBOX[4];
};

struct ScenaricResource {
    // model type of this resource needs to be 3 or 4
    u16 modelResourceIdx;
    // model type needs to be 3
    // none if 0xFFFF
    u16 lowPolyModelResourceIdx;
    s16 x, y, z;
    u16 classID; // see Lvl-03/Scenaric_Classes.h
    // 1024 = 90 degrees
    s16 pitch, yaw, roll;
    u16 pad;

    // class property data
    match (classID) {
        (1): SamClass sam;
        (70): VisibilityManagerClass visibilityManager;
        (86): TimeMachineSphereClass timeMachineSphere;
    }
};

struct Export {
    u16 id;
    u16 count;

    u32 offsets[count];
};

struct ExportSection {
    u32 count;
    Export exports[count];
};

u32 maxCollPolygonIndex = 0;

struct CollisionMapIndex {
    u32 index;

    if (index > maxCollPolygonIndex) {
        maxCollPolygonIndex = index;
    }
};

struct CollisionMapCell {
    u32 polygonCount;
    CollisionMapIndex polygonIndexes[polygonCount];
};

struct CollisionMapSection {
    u16 gridWidth, gridHeight;
    s16 worldMinX, worldMinY;
    s16 cellWidth, cellHeight;

    // this should be a matrix, but imhex doesn't support those and i'm lazy
    CollisionMapCell cells[gridHeight * gridWidth];
};

struct CollisionVertex {
    s16 X, Y, Z;
};

struct CollisionPolygon {
    s16 minX, minY, minZ;
    s16 maxX, maxY, maxZ;

    s16 normalX, normalY, normalZ;

    // seems to always be 0 on every level
    u16 surfaceFlags;

    CollisionVertex vertices[3];

    u16 pad;
};

struct CollisionPolygonSection {
    CollisionPolygon polygons[maxCollPolygonIndex + 1];
};

// all of these seem to be ExportTex IDs?
struct ConfigSection {
    u16 unk1, unk2, unk3, unk4, unk5, unk6;
};

struct ScreenMapSection {
    u16 gridWidth, gridHeight;
    u16 unk1;
    u16 unk2;
    u16 unk3;
};

struct DebugAniAnim {
    u16 id;
    char name[];

    // 16-bit alignment
    if (($ % 2) == 1) {
        padding[1];
    }
};

struct DebugAniModel {
    u16 id;
    u16 count;
    char name[];

    // 16-bit alignment
    if (($ % 2) == 1) {
        padding[1];
    }

    DebugAniAnim animations[count];
};

struct DebugAniSection {
    u16 count;
    DebugAniModel models[count];
};

u24 CollPolygonSectionOffset;

struct Resource {
    u24 pointer;
    u8 flags;

    match (flags) {
        // used for objects in level (ralph, mailbox, etc etc)
        (5): ScenaricResource scenaricRes @ pointer;
        // these two are used for level geometry collision
        // CollisionMapSection is a grid with indices to polygons in CollisionPolygonSection
        (0x80): CollisionMapSection collMap @ pointer;
        (0x81): {
            //CollisionPolygonSection collPolygons @ pointer;
            CollPolygonSectionOffset = pointer;
        }
        // see Lvl-03/GameRes.h
        // IDs from here are usually used as properties in scenaric classes
        (0x82): ExportSection exportSection @ pointer;
        (0x83): ConfigSection config @ pointer;
        (0x84): ScreenMapSection screenMap @ pointer;
        (0x85): CinExpHeader cinExpHeader @ pointer;
        // used for debug logging, lol
        // "Cannot find AnimID %d, Model %s, Anim %s\n"
        (0x86): DebugAniSection debugAni @ pointer;
    }

    // model stuff
    u8 modelType = flags & 0xbf;
    match (modelType) {
        (3 | 11): Model model @ pointer;
        (4): AnimatedModel animated_model @ pointer;
        (38): Model skybox @ pointer;
    }

    match (modelType) {
        (3 | 4 | 11 | 38): bool isLevelGeometry = (flags & 0x40) == 0 [[export]];
    }
};

struct WAR {
    type::Hex<u32> crc32;
    char version[4];
    u8 fogRed, fogGreen, fogBlue, unused;
    u32 resource_num;

    Resource resources[resource_num];
};

WAR war @ 0;
CollisionPolygonSection collPolygons @ CollPolygonSectionOffset;