# Fonts

The HUD is designed for **Bebas Neue** (headers) and **Space Mono** (body), but
ships with system fallbacks (Bahnschrift Condensed / Consolas) so it looks right
out of the box without bundling any font files.

To use the real fonts:

1. Drop the `.ttf` files here, e.g. `BebasNeue-Regular.ttf`, `SpaceMono-Regular.ttf`.
2. Mark them as `Resource` in the build (add to `Onyx.App.csproj`):
   ```xml
   <ItemGroup>
     <Resource Include="Assets/Fonts/*.ttf" />
   </ItemGroup>
   ```
3. Point the theme at them in `Themes/Brutalist.xaml`:
   ```xml
   <FontFamily x:Key="HeaderFont">/Onyx;component/Assets/Fonts/#Bebas Neue</FontFamily>
   <FontFamily x:Key="BodyFont">/Onyx;component/Assets/Fonts/#Space Mono</FontFamily>
   ```

The `#Name` after the path is the font's internal family name, not the filename.
