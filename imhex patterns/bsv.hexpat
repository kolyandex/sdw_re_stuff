#pragma endian little

import type.magic;

struct Filename {
    u32 size;
    char string[size];
};

struct BSV {
    type::Magic<"BSV_FILEV2.5"> magic;
    Filename filenames[0x74];
};

BSV bsv @ 0;