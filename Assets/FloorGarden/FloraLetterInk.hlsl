#ifndef FLORA_LETTER_INK_INCLUDED
#define FLORA_LETTER_INK_INCLUDED
// One bronze ink recipe for handwritten strokes and interaction captions.
half3 FloraLetterInk(half shimmer,half rim)
{
    half3 bronze=lerp(half3(.085,.042,.026),half3(.23,.105,.048),shimmer*.45h);
    return lerp(bronze,half3(.36,.19,.055),rim);
}
#endif
