# Comandos de cuentas de CodexBar para Windows

Esta es una referencia rápida para administrar las cuentas que CodexBar lee en
Windows. Los perfiles son independientes: usar un bloque con `-2` afecta solo
la segunda cuenta; no la principal.

> Ejecuta los bloques en PowerShell. No hace falta estar dentro de la carpeta
> del proyecto. Al terminar cada bloque, cierra esa ventana de PowerShell o
> ejecuta la última línea para limpiar la variable temporal.

## Nombres de perfiles

| Cuenta | Codex | Claude Code |
| --- | --- | --- |
| Principal | `%USERPROFILE%\.codex` | `%USERPROFILE%\.claude` |
| Segunda | `%USERPROFILE%\.codex-2` | `%USERPROFILE%\.claude-2` |
| Perfil personalizado `trabajo` | `%USERPROFILE%\.codex-trabajo` | `%USERPROFILE%\.claude-trabajo` |

Sustituye `2` por el nombre real si creaste un perfil personalizado con
`Add-CodexBarAccounts.ps1 -ProfileName`.

## Codex

### Revisar la cuenta principal

```powershell
codex login status
```

### Iniciar sesión o cambiar la cuenta principal

```powershell
codex logout
codex login --device-auth
codex login status
```

Codex mostrará una URL y un código de dispositivo. Abre la URL en el perfil
del navegador de la cuenta deseada e introduce el código allí.

### Revisar, salir o volver a entrar en la segunda cuenta

```powershell
$env:CODEX_HOME = "$env:USERPROFILE\.codex-2"
codex login status
Remove-Item Env:CODEX_HOME
```

```powershell
$env:CODEX_HOME = "$env:USERPROFILE\.codex-2"
codex logout
codex login --device-auth
codex login status
Remove-Item Env:CODEX_HOME
```

Para otro perfil, por ejemplo `trabajo`, cambia `.codex-2` por
`.codex-trabajo`.

## Claude Code

### Revisar la cuenta principal

```powershell
claude auth status
```

### Salir y volver a entrar en la cuenta principal

```powershell
claude auth logout
claude auth login
claude auth status
```

### Revisar, salir o volver a entrar en la segunda cuenta

```powershell
$env:CLAUDE_CONFIG_DIR = "$env:USERPROFILE\.claude-2"
claude auth status
Remove-Item Env:CLAUDE_CONFIG_DIR
```

```powershell
$env:CLAUDE_CONFIG_DIR = "$env:USERPROFILE\.claude-2"
claude auth logout
claude auth login
claude auth status
Remove-Item Env:CLAUDE_CONFIG_DIR
```

Al iniciar sesión, Claude abre una URL. Ábrela con el navegador que tenga la
cuenta correcta. Si solicita un código al volver a PowerShell, pega únicamente
el código mostrado por la página de autorización: no pegues la URL completa.

Para otro perfil, por ejemplo `trabajo`, cambia `.claude-2` por
`.claude-trabajo`.

## Crear un nuevo par de perfiles

El asistente crea un perfil de Codex y otro de Claude con el mismo sufijo y
guía ambos inicios de sesión:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass `
  -File "$env:LOCALAPPDATA\Programs\CodexBar\Add-CodexBarAccounts.ps1" `
  -ProfileName trabajo `
  -Login
```

Para crear el perfil estándar `-2`, omite `-ProfileName trabajo`.

## Actualizar los datos en CodexBar

Al terminar cualquier inicio de sesión, abre CodexBar y selecciona **Refresh**.
Las tarjetas correspondientes deben mostrar los datos de la cuenta recién
autenticada.

Cada tarjeta también tiene **Copy login** (o **Copy** en Summary). Ese botón
copia el bloque de inicio de sesión para *esa* cuenta concreta, sin incluir
contraseñas ni tokens.

## Rehacer el paquete ZIP desde el repositorio

En la PC de compilación, desde el repositorio:

```powershell
cd $env:USERPROFILE\source\CodexBar-win
git pull origin main

powershell.exe -NoProfile -ExecutionPolicy Bypass `
  -File .\Windows\Publish-Windows.ps1 `
  -ArchiveOnly `
  -ArchivePath "$env:USERPROFILE\Desktop\CodexBar-Windows.zip"
```

El comando compila, ejecuta las pruebas y deja el paquete final en el
Escritorio. Para una reconstrucción rápida, ya verificada previamente, agrega
`-SkipTests` antes de `-ArchiveOnly`.
