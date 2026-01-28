#pragma endian little

u16 NUM_SECTIONS;

struct Metadata {
    u32 crc32;
    char version[4];
    u16 num_languages;
    u16 num_sections;
    NUM_SECTIONS = num_sections;
};

struct String {
    char string[];
};

struct Section {
    u8 num_strings;
    String     strings[num_strings];
};

struct Language {
    Section sections[NUM_SECTIONS];
};

struct MLT {
    Metadata metadata;
    
    u32 crc32;
    Language languages[metadata.num_languages];
};

MLT mlt @ 0;