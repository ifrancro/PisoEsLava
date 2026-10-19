# Integración del personaje masculino

**Objetivo:** usar el nuevo personaje en solo y LAN con estados de animación conectados
al movimiento, cambios de tamaño, impactos, victoria y reaparición.

**Diseño aprobado:** propuesta de seis pasos aceptada por el usuario en este chat.
Unity 2022.3, Netcode 1.12.2, Animator Generic existente; sin root motion.

**Ejecución:** en el proyecto actual para conservar escenas y modelo todavía sin commit.
No se regeneran el mapa ni los menús. Las modificaciones se aplican con APIs de Unity.

- [x] Escribir y ejecutar una validación que falle porque solo/LAN siguen usando el modelo anterior.
- [x] Conectar el controlador al componente MaleCharacterView y retirar la animación procedural
      para este rig; tomar movimiento real descontando el transporte de plataformas.
- [x] Sincronizar Animator desde el propietario mediante NetworkAnimator, incluidos triggers;
      mantener Form como autoridad del tamaño físico y recolorear únicamente la ropa.
- [x] Vincular golpes, cambios de tamaño aceptados, victoria y reset de reaparición.
- [x] Reemplazar las imágenes en la escena individual y el prefab Jugador_LAN,
      manteniendo GUID, cápsula y referencias de red. Validar ambos escenarios.
- [x] Ejecutar pruebas de estados y física en Play Mode, incluida caída desde una plataforma,
      crecimiento bloqueado, escalas .46/1/1.55, soporte móvil y reinicio.
- [x] Comprobar propagación de red con host/cliente local, colores y ausencia de errores.

**Pruebas:** harness de Editor en Assets/TowerRush/Editor/Characters; informes y capturas
en Logs/CharacterIntegration. Antes de modificar escenas se guardan copias exactas en Logs.
La prueba de Animator debe observar los estados reales y huesos, no sólo parámetros.

**Riesgos a revisar:** carrera visual al viajar sobre plataformas; caída sin saltar;
respawn durante una transformación o celebración; bucles de triggers; color de piel;
pausa individual y celebración con Time.timeScale=0; objetos visuales en raycasts de cámara.

## Evidencia

- RED: `CharacterIntegrationRed.log`, error esperado «new male character is not installed».
- Instalación: `CharacterIntegrationInstall.log`, mapa sin cambios y validación positiva.
- Play Mode: 11 comprobaciones pasaron; se observan estados reales del Animator, tamaños,
  física y retorno al checkpoint. Un primer fallo de la prueba de victoria se debía a
  cambiar updateMode y observar el estado dentro del mismo frame; se comprobó esperando
  frames reales, sin cambiar la lógica de producción para satisfacer la prueba.
- Ajuste de materiales: los colores lineales de Blender se convierten a paleta sRGB
  para Standard y se remapean a materiales editables. No se modifica la iluminación del mapa.
- Resultado final: 12 comprobaciones de Play Mode, validación completa de ambas escenas
  (49 plataformas y 3 checkpoints), compilación Windows y dos procesos reales host/cliente.
  Ambos observaron los 10 estados locales y remotos y las tres formas; colores y materiales
  correctos. Informes finales de red: `Logs/CharacterIntegration/Graphics/network_*.json`.
- Una prueba inicial sin dispositivo gráfico observó correctamente animaciones y formas,
  pero no puede verificar Material.HasProperty/colores porque Standard no está soportado
  por Null Graphics. Se confirmó con renderizado real; no se cambió producción para ese artefacto.
- Revisión final: sin root motion, sin cambio de cápsulas/recorrido, triggers sólo desde
  el propietario, colores por submesh, compatibilidad de modelos antiguos y protocolo 3.2.
