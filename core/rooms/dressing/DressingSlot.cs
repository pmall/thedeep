using System;
using System.Collections.Generic;

namespace TheDeep;

/// <summary>A slot in a row of dressing pieces: its index along the row, the variant to use, and whether it lies toppled.</summary>
public readonly record struct DressingSlot(int Index, int Variant, bool Toppled);
