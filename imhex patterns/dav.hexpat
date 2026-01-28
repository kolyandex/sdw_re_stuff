#pragma endian little

import type.magic;
import type.color;
import std.io;
import std.math;

u32 texture_ptr;

struct Texture {
    u16 x;
    u16 width;
    u16 y;
    u16 height;
    u16 page_idx;
};

struct MatOrTex {
    u32 ptr;
    if (ptr >= texture_ptr) {
        Texture texture @ ptr;
    } else {
        u16 material_idx @ ptr;
        Texture texture @ texture_ptr + (material_idx * 10);
    }
};

struct ExportTex {
    u16 index;
    u16 size;
    MatOrTex stuff[size];
};

struct ExportTexSection {
    u32 size;
    ExportTex exp_textures[size];
};

struct Page {
    u16 x1;
    u16 x2;
    u16 y1;
    u16 y2;
    
    u16 flags;
    
    s32 width = std::math::abs(s32(x1) - s32(x2));
    s32 height = std::math::abs(s32(y1) - s32(y2));
    
    u16 argb5551_or_argb4444_data[height * width] [[sealed]];
    bool is_argb5551 = (flags & 0x1c) == 4 [[export]];
};

struct DAVHeader {
    u16 material_size;
    u16 texture_size;
    u16 page_size;
    u32 material_pointer;
    u32 texture_pointer;
    u32 page_pointer;
    u32 exp_tex_pointer;
    
    texture_ptr = texture_pointer;
    
    Page page_section[page_size] @ page_pointer;
    ExportTexSection exp_tex_section @ exp_tex_pointer;
};

struct DAV {
    type::Magic<"VDX7"> magic;
    type::Magic<"CHEKCHEKCHEKCHEK"> pad;
    u32 header_pointer;
    DAVHeader header @ header_pointer;
};

DAV dav @ 0;
