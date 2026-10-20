[Unreleased]
- Expand all 13 target-typed constructors for Unity 2020.3's C# 8 compiler.
- Preserve UTF-8 marshalling attributes without relying on the LPUTF8Str enum name missing from Unity 2020.3's .NET Standard 2.0 reference assemblies.
- Declare TextMeshPro, uGUI, and image-conversion dependencies. Newer Editors resolve their built-in TMP compatibility package and uGUI implementation.
- Add the explicit UnityEngine.UI assembly reference and isolate the UnityEditor import from standalone builds.
- Replace the beta minimum-version suffix with the intended Unity 2020.3.0f1 stable target.
- Preserve the Muxy.Gateway assembly, public type ownership, and existing asset GUIDs.
- Add an isolated package-import/custom-assembly compilation harness and record scoped validation results.

[0.1.0] 2022/10/04
Initial release
