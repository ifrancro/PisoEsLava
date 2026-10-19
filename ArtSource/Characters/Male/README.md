# Personaje masculino — Heat Rise

Modelo original inspirado en la referencia del usuario: cabeza grande, nariz angular,
barba, sombrero y ropa lisa. Altura de referencia: 1,75 m desde la planta del pie.

## Archivos

- `MaleCharacter.blend`: fuente editable con rig, acciones, UV y estudio de render.
- `preview.png`: render del personaje.
- `animations.mp4`: demostración de acciones. El desplazamiento del salto y la escala
  total de esta película son una simulación de lo que hará el código del juego.
- `UV_Layout.png`: atlas UV compartido para inspeccionar los cortes.
- `blender_validation.json` / `unity_validation.json`: comprobaciones del modelo y su importación.
- FBX: `Assets/TowerRush/Models/MaleCharacter/MaleCharacter.fbx`.
- Prefab: `Assets/TowerRush/Models/MaleCharacter/Unity/MaleCharacter.prefab`.

## Edición en Blender

Escena `HR_Male_Character`, rig `HR_Male_Rig`. El estudio no forma parte del FBX.
Selecciona el rig y abre **Dope Sheet > Action Editor** para elegir una acción.
La malla se construyó directamente con topología de baja densidad, bucles en codos y
rodillas, pesos normalizados, cortes UV y empaquetado en UV0. No se utilizó un modelo
de alta densidad ni una reducción automática de polígonos.

Las seis mallas separan ojos, cara, cabello/barba, sombrero, ropa y piel del cuerpo.
Los materiales `HR_Clothing_Shirt` y `HR_Clothing_Pants` admiten cambios de color;
`Hat` se puede ocultar. Huesos `Socket_Hat`, `Socket_Back` y `Socket_Hand.L/R`
permiten accesorios sencillos. Piel, ojos y cabello conservan sus colores.

## Animaciones

Todas usan 30 fps, rig **Generic** y movimiento en el sitio, sin root motion:

| Acción | Fotogramas | Uso |
|---|---:|---|
| Idle | 1–60 | Reposo en bucle |
| Run | 1–24 | Carrera en bucle |
| Jump | 1–24 | Impulso y postura del salto |
| Fall | 1–40 | Caída con brazos abiertos, en bucle |
| Land | 1–16 | Amortiguación al aterrizar |
| Grow | 1–36 | Anticipación y expansión corporal |
| Shrink | 1–30 | Encogimiento corporal |
| ReturnNormal | 1–30 | Recuperación del tamaño normal |
| Hit | 1–20 | Reacción a un golpe |
| Victory | 1–72 | Celebración en bucle |

`Grow`, `Shrink` y `ReturnNormal` animan el gesto y una deformación pequeña, con
escala del hueso Root igual a 1. El juego aplica la escala completa **1 / 0,46 / 1,55**
en el objeto visual y ajusta su cápsula. Así no se duplica la transformación.

## Uso desde Unity

El prefab ya incluye Animator, controller y `HeatRise.Characters.MaleCharacterView`.
El menú **Heat Rise > Personajes > Preparar personaje masculino** permite preparar
los assets después de una nueva exportación. Conserva ediciones posteriores del
prefab/controller. **Validar personaje masculino** vuelve a comprobar la importación.

Interfaz visual utilizada por el controlador:

```csharp
view.SetLocomotion(horizontalSpeed / moveSpeed, grounded, verticalSpeed);
view.PlayGrow();         // Después de aceptar la transformación gigante.
view.PlayShrink();       // Después de aceptar la transformación pequeña.
view.PlayReturnNormal();
view.PlayHit();
view.PlayVictory();
view.SetClothingColors(shirtColor, pantsColor);
view.SetHatVisible(false);
view.ResetPose();        // Tras reaparecer o terminar la celebración.
```

El personaje está integrado en `HeatRise_Jugable` y en `Jugador_LAN.prefab`. Un solo
rig cubre los tres tamaños. `PlayerController` envía al Animator el desplazamiento
real descontando el transporte de plataformas, el contacto con suelo y la velocidad
vertical; los cambios de tamaño aceptados, golpes y victoria activan sus gestos.
La animación procedural anterior se usa sólo como compatibilidad para modelos antiguos.

`OwnerNetworkAnimator` replica parámetros, estados y triggers desde el propietario.
`Form` sigue controlando la cápsula y escala de cada jugador. El color de red afecta
solamente camisa y pantalón. La celebración individual usa tiempo sin escalar para
continuar cuando se congela la partida; el respawn restablece Idle y tamaño normal.
El protocolo LAN es ahora 3.2: todos los participantes necesitan la misma compilación.

Los materiales editables están en `Models/MaleCharacter/Unity/Materials`; conservan
los colores planos de Blender y una contribución ambiental tenue para las zonas en
sombra. El instalador mantiene cambios posteriores de esos materiales y del modelo.
Menú de instalación: **Heat Rise > Personajes > Integrar personaje en el juego**.
Las copias previas de escena y prefab se guardan en `Logs/CharacterIntegration/Backups`.
Las pruebas y capturas de la integración se guardan en `Logs/CharacterIntegration`.

## Reconstrucción

En un archivo nuevo de Blender: ejecutar `build_male.py`, `animate_male.py` y
`export_validate_male.py`, en ese orden. Los scripts contienen la ruta local del proyecto.
`render_showcase.py` genera las imágenes temporales de la película en `Logs`.
