# Changelog

## 1.6.0

- Nuevo selector **Orden de las cuentas** en Personalizar: Original, Con cuota primero
  y Más cuota disponible. La elección se guarda y se aplica al instante en todas las vistas.
- **Más cuota disponible** compara el menor saldo real entre Sesión y Semanal, de mayor
  a menor; la otra cuota desempata. Las cuentas agotadas, sin datos o con errores quedan abajo.
- Se conserva la preferencia de orden anterior al actualizar. El cálculo no confunde
  los porcentajes de espera de las cuentas agotadas con cuota disponible.

## 1.5.0

- Las cuotas agotadas muestran una barra de espera que se vacía hasta la renovación:
  roja al 100 %, amarilla al 50 % y verde desde el 25 %, con tonos intermedios.
- El porcentaje indica el tiempo restante respecto al período completo de la cuota,
  tanto en Sesión como en Semanal. Se actualiza cada minuto, en todas las vistas y gráficos.
- La etiqueta y el tooltip distinguen tiempo de espera de cuota disponible. La tarjeta
  sigue resaltada en rojo hasta que el proveedor confirme que la cuota volvió.
- Sin fecha o duración de renovación conocida se conserva el indicador de uso habitual.

## 1.4.0

- La elección entre cuenta regresiva y fecha exacta se conserva por cuenta y cuota, incluso
  después de actualizar los datos o reiniciar CodexBar.
- Fechas exactas con día abreviado, fecha y hora local, por ejemplo `VIE-20NOV, 12:00PM`.
- Resets disponibles de Claude, con detección de la versión instalada de Claude Code.
- Uso extra en violeta: gasto/límite mensual en USD de Claude y saldo reportado en créditos de Codex.
- Actualización automática desde GitHub Releases al instalar con Setup.exe; las descargas
  pendientes se aplican al reiniciar la app, con opción para reiniciar inmediatamente.
- Refresco del icono de la barra de tareas al crearse el botón de Explorer.

## 1.3.0

- **Resets de Codex**: cada cuenta de Codex muestra los resets de límite disponibles en ChatGPT, con su
  vencimiento en el tooltip. Se resalta si alguno vence en 3 días o menos. Solo lectura: CodexBar no los canjea.
- **Opacidad de la ventana** configurable en Personalizar (40 % a 100 %).

## 1.2.0

- Nueva sección **Cuentas** en Personalizar: botones para agregar cuentas de Codex o Claude Code.
  Eligen el siguiente perfil libre, copian el comando de PowerShell para iniciar sesión y permiten
  abrirlo directo en PowerShell.
- "Copiar login" de las tarjetas usa el mismo comando, ahora con instrucciones en el idioma de la app.

## 1.1.0

- La app está disponible en inglés. Nueva opción **Idioma** en Personalizar: Automático (según el
  idioma de Windows), Español o English. Cambia al instante, sin reiniciar.
- Capturas del README en ambos idiomas.
- Política de privacidad en el README y metadatos de producto en el ejecutable.

## 1.0.2

- Bordes nítidos: se quitó la sombra que hacía renderizar la ventana como imagen y se alinean las
  líneas a píxeles enteros, también con escalado de Windows.
- La lista de cuentas se desplaza cuando no entra en pantalla (pantallas chicas o escala alta), con
  una barra de scroll fina acorde al tema.
- "Medianoche" entra completo en su tile de Personalizar.

## 1.0.1

- El botón **Actualizar** pasa al pie de la ventana, más grande y visible en todas las vistas
  (también en Mini). Gira y muestra "Actualizando…" mientras consulta.

## 1.0.0

Primera versión pública de CodexBar para Windows, fork de
[steipete/CodexBar](https://github.com/steipete/CodexBar).

- Límites de sesión y semanales de Codex y Claude Code, con varias cuentas por proveedor.
- Vistas Tarjetas, Resumen y Mini.
- Personalización: temas, color de acento, tipos de gráfico (barras, anillos, medidor, bloques,
  números), colores por nivel o proveedor, densidad y escala.
- Marcador de ritmo, avisos cuando vuelve la cuota e ícono dinámico en la bandeja.
- Instalador (Setup.exe) con actualizaciones automáticas desde GitHub Releases.
- Iniciar con Windows.
