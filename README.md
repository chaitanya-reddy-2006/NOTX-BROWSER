# NOTX Browser

A lightweight Windows desktop browser shell built with C#, WPF and Microsoft Edge WebView2.

## Requirements

- Windows 10/11
- .NET 8 SDK
- Microsoft Edge WebView2 Runtime (usually present on Windows 11)

## Run it

1. Install the .NET 8 SDK: https://dotnet.microsoft.com/download/dotnet/8.0
2. Open PowerShell in this folder.
3. Run:

   ```powershell
   dotnet restore
   dotnet run
   ```

## Build a release

```powershell
dotnet publish -c Release -r win-x64 --self-contained false
```

The published application will be under `bin\\Release\\net8.0-windows\\win-x64\\publish\\`.

## Shortcuts

- Ctrl + L: focus address bar
- Ctrl + T: new tab
- Ctrl + W: close current tab (when more than one tab is open)
- F5: reload page
- Enter: navigate/search

## Notes

- Google is the default home page and search provider.
- Website rendering is provided by the installed Microsoft Edge WebView2 Runtime.
- This is a starter browser. It does not yet include bookmarks, a downloads manager, ad blocking, or private-browsing controls.
- WebView2 Runtime is a separate Microsoft component and may need installation on some PCs.
