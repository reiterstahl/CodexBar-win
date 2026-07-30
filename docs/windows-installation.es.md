# Instalación de CodexBar en una PC nueva

Esta guía instala CodexBar en Windows 11 x64 a partir del archivo
`CodexBar-Windows.zip`. La PC de destino no necesita Swift, .NET SDK,
Visual Studio, Git ni el repositorio del proyecto.

CodexBar no incluye ni transfiere credenciales. Codex CLI y Claude Code
administran sus propias sesiones, por lo que cada cuenta debe autenticarse
nuevamente en la PC nueva.

## 1. Requisitos

- Windows 11 de 64 bits.
- Una conexión a Internet para instalar los CLI y autenticar las cuentas.
- `CodexBar-Windows.zip`.
- Opcionalmente, `CodexBar-Windows.zip.sha256` para verificar la transferencia.

CodexBar incluye los runtimes de .NET y Swift que necesita. Si Windows informa
que falta un runtime de Microsoft Visual C++, instala el
[Microsoft Visual C++ Redistributable x64](https://aka.ms/vs/17/release/vc_redist.x64.exe)
y vuelve a intentarlo.

## 2. Instalar Codex CLI

Abre PowerShell y ejecuta el instalador oficial para Windows:

```powershell
powershell.exe -ExecutionPolicy Bypass -Command `
  "irm https://chatgpt.com/codex/install.ps1 | iex"
```

Cierra y abre PowerShell nuevamente y comprueba la instalación:

```powershell
codex --version
```

Autentica la cuenta principal mediante código de dispositivo:

```powershell
codex login --device-auth
```

Codex mostrará una URL y un código. Abre la URL en el perfil de navegador de
la cuenta correcta, introduce el código y regresa a PowerShell. Si el método
no está habilitado, activa **Device code login** en la configuración de
seguridad de ChatGPT.

Comprueba el estado:

```powershell
codex login status
```

## 3. Instalar Claude Code

En PowerShell, ejecuta el instalador oficial recomendado:

```powershell
irm https://claude.ai/install.ps1 | iex
```

También se puede instalar mediante WinGet:

```powershell
winget install Anthropic.ClaudeCode
```

Cierra y abre PowerShell nuevamente y comprueba la instalación:

```powershell
claude --version
```

Inicia Claude Code:

```powershell
claude
```

Completa el acceso en el navegador. Si no aparece automáticamente, escribe
`/login`. Cuando termine la autenticación, escribe `/exit`.

## 4. Extraer e instalar CodexBar

Copia `CodexBar-Windows.zip` a la PC nueva. Si también recibiste el archivo
`.sha256`, puedes mostrar el hash descargado con:

```powershell
(Get-FileHash .\CodexBar-Windows.zip -Algorithm SHA256).Hash.ToLowerInvariant()
Get-Content .\CodexBar-Windows.zip.sha256
```

Los dos valores deben coincidir. Después, desbloquea y extrae el ZIP:

```powershell
Unblock-File .\CodexBar-Windows.zip

$setupDirectory = Join-Path $env:TEMP `
  ("CodexBar-Setup-" + [guid]::NewGuid().ToString("N"))

Expand-Archive `
  -Path .\CodexBar-Windows.zip `
  -DestinationPath $setupDirectory
```

Instala CodexBar con acceso directo de Escritorio e inicio automático:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass `
  -File "$setupDirectory\Install-CodexBar.ps1" `
  -DesktopShortcut `
  -StartWithWindows
```

El instalador:

- verifica todos los hashes internos del paquete;
- prueba el motor sin consultar las cuentas;
- instala en `%LOCALAPPDATA%\Programs\CodexBar`;
- crea accesos directos en Inicio y Escritorio;
- registra el inicio automático solo para el usuario actual;
- inicia CodexBar en el área de notificación.

No necesita permisos de administrador. Los parámetros `-DesktopShortcut` y
`-StartWithWindows` son opcionales.

## 5. Añadir las cuentas secundarias

El asistente incluido crea perfiles independientes para la segunda cuenta de
Codex y la segunda cuenta de Claude:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass `
  -File "$env:LOCALAPPDATA\Programs\CodexBar\Add-CodexBarAccounts.ps1" `
  -Login
```

El perfil predeterminado es `2`:

```text
%USERPROFILE%\.codex-2
%USERPROFILE%\.claude-2
```

En el paso de Codex, la terminal mostrará una URL y un código sin abrir el
navegador predeterminado. Completa el acceso con la segunda cuenta. Después
se abrirá Claude Code para autenticar la segunda cuenta; usa `/login` si es
necesario y `/exit` cuando termine.

Para crear perfiles adicionales, proporciona un nombre corto:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass `
  -File "$env:LOCALAPPDATA\Programs\CodexBar\Add-CodexBarAccounts.ps1" `
  -ProfileName trabajo `
  -Login
```

## 6. Comprobar la instalación

Abre **CodexBar** desde el menú Inicio o desde su icono en la bandeja y
presiona **Refresh**. Con dos cuentas por proveedor deben aparecer:

1. Codex principal.
2. Codex secundario.
3. Claude principal.
4. Claude secundario.

Puedes hacer clic en el nombre de cada cuenta para cambiarlo. El botón **Aa**
ajusta la escala de la interfaz entre 80% y 160%; **Summary** activa la vista
apaisada y `◆` activa o desactiva “siempre encima”.

Cada tarjeta incluye **Copy login**. Copia al portapapeles el bloque de
PowerShell correcto para esa cuenta concreta, incluida su carpeta aislada. No
incluye contraseñas, tokens ni otros secretos. En Summary el botón se muestra
como **Copy**.

Para una referencia de los comandos de inicio/cierre de sesión por perfil,
consulta [Comandos de cuentas de CodexBar para Windows](windows-account-commands.es.md).

Para comprobar el motor directamente:

```powershell
& "$env:LOCALAPPDATA\Programs\CodexBar\CodexBarWindowsEngine.exe" --pretty
```

El resultado debe incluir los proveedores `codex` y `claude`. El motor no
imprime tokens ni contraseñas.

## 7. Actualizar CodexBar

Extrae un ZIP nuevo en otra carpeta y ejecuta nuevamente su instalador:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass `
  -File .\Install-CodexBar.ps1 `
  -DesktopShortcut `
  -StartWithWindows
```

El instalador cierra la versión anterior y la reemplaza. Conserva las cuentas,
los nombres personalizados, la escala, la posición y las demás preferencias.
No es necesario volver a autenticar.

## 8. Desinstalar

Usa **Uninstall CodexBar** en el menú Inicio. Esto elimina la aplicación y los
accesos directos, pero conserva preferencias y credenciales.

Para eliminar también las preferencias visuales de CodexBar:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass `
  -File "$env:LOCALAPPDATA\Programs\CodexBar\Uninstall-CodexBar.ps1" `
  -RemoveSettings
```

La desinstalación nunca elimina `%USERPROFILE%\.codex*` ni
`%USERPROFILE%\.claude*`.

## Solución de problemas

### `codex` o `claude` no se reconoce

Cierra todas las ventanas de PowerShell y abre una nueva. Después ejecuta:

```powershell
Get-Command codex
Get-Command claude
```

Si alguno sigue sin aparecer, reinstala ese CLI con el comando de esta guía.

### SmartScreen muestra una advertencia

El paquete todavía no está firmado. Verifica el archivo `.sha256`, selecciona
**Más información** y ejecuta la aplicación solo si el ZIP proviene de una
fuente de confianza.

### Las cuentas nuevas no aparecen

Presiona **Refresh**. Si no aparecen, cierra CodexBar mediante **Exit** y
ábrelo desde Inicio. Comprueba también el motor con `--pretty`.

### La aplicación instalada no inicia

Cierra cualquier proceso anterior:

```powershell
Get-Process CodexBar.Windows.Tray -ErrorAction SilentlyContinue |
  Stop-Process -Force
```

Extrae nuevamente el ZIP en una carpeta vacía y vuelve a ejecutar
`Install-CodexBar.ps1`.

## Crear el ZIP transferible

Este paso solo se realiza en la PC de compilación, donde ya están instalados
Swift y .NET SDK:

```powershell
cd $env:USERPROFILE\source\CodexBar-win
git pull origin main

powershell.exe -NoProfile -ExecutionPolicy Bypass `
  -File .\Windows\Publish-Windows.ps1 `
  -SkipTests `
  -ArchiveOnly `
  -ArchivePath "$env:USERPROFILE\Desktop\CodexBar-Windows.zip"
```

Para la PC nueva solo se transfieren `CodexBar-Windows.zip` y, opcionalmente,
`CodexBar-Windows.zip.sha256`.

## Referencias oficiales

- [Instalación y repositorio de Codex CLI](https://github.com/openai/codex)
- [Autenticación de Codex en dispositivos sin navegador](https://learn.chatgpt.com/docs/auth#login-on-headless-devices)
- [Instalación y repositorio de Claude Code](https://github.com/anthropics/claude-code)
