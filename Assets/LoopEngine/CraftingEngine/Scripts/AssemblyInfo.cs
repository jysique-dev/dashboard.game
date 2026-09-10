using System.Runtime.CompilerServices;

// The edit-mode toolkit must write identity and content fields that runtime code has no
// business touching, so those mutators are internal and this is the only grant.
//
// Two names are listed because the toolkit can live in either assembly:
//   - LoopEngine.Crafting.Editor : when the Editor folder has its own .asmdef
//   - Assembly-CSharp-Editor     : Unity's predefined editor assembly, used when there is
//                                  no .asmdef and the scripts simply sit in an Editor folder
//
// Naming an assembly that does not exist in the project is harmless: the attribute is only
// checked when an assembly with that name actually asks for access.
[assembly: InternalsVisibleTo("LoopEngine.CraftingEngine.Editor")]
[assembly: InternalsVisibleTo("Assembly-CSharp-Editor")]
