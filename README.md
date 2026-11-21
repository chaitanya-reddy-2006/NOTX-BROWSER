# NOTX Browser

A Windows desktop browser built with C#, WPF and Microsoft Edge WebView2. This edition adds a custom NOTX brand mark, a refreshed dark interface, built-in request filtering, pop-up controls and browser essentials.

## Features

- Multi-tab browsing; open links in new tabs when sites request a new window and pop-ups are allowed
- Back, forward, reload, home, address/search bar
- Bookmarks saved locally and a browsing history list (up to 1,000 recent navigations)
- Download activity list with links to downloaded files
- Built-in ad request blocking with a live blocked-request counter
- Pop-up/new-window blocking with a live blocked-pop-up counter
- Independent ON/OFF controls for ad blocking and pop-up blocking
- Keyboard shortcuts: Ctrl+L address bar, Ctrl+T new tab, Ctrl+W close tab, Ctrl+D bookmark, Ctrl+H history, Ctrl+J downloads, F5 reload
- Data stored locally under `%LOCALAPPDATA%\NOTXBrowser`

## Requirements

- Windows 10/11
- .NET 8 SDK
- Microsoft Edge WebView2 Runtime

## Run

Open PowerShell in this folder and run:

```powershell
dotnet restore
dotnet run
```

## Publish

```powershell
dotnet publish -c Release -r win-x64 --self-contained false
```

The publish output is under `bin\Release\net8.0-windows\win-x64\publish\`.

## Ad-blocking and privacy notes

The built-in blocker uses a small bundled list of common advertising domains and URL patterns. It blocks matching WebView2 network requests and counts each blocked request; it is not yet a full EasyList/uBlock Origin replacement, cosmetic filter, tracker-protection engine, or extension platform. Some ads may still appear and a broad URL rule can occasionally affect page content. Turn the blocker off for a site that does not work correctly.

The pop-up control blocks WebView2 new-window requests. It does not guarantee suppression of every in-page modal or notification prompt. History and bookmarks are stored locally; this build does not yet provide a dedicated private/incognito profile or synchronisation.

Downloads are handled by WebView2 using its normal download destination. The downloads panel tracks activity and can open completed files when the saved path is available.
