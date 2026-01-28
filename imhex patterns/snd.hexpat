#pragma endian little

import type.magic;
import type.base;

struct Sound {
    u32 id;
    u32 size;
    u32 unused;
    
    u8 data[size] [[sealed]];
};

struct SND {
    type::Hex<u32> crc32_unused;
    type::Magic<"vdx7"> magic;
    u32 num_sounds;
    Sound sounds[num_sounds];
};

SND snd @ 0;