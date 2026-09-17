using System.Text;

namespace HeatRise.UI
{
    /// <summary>Texto de la pantalla de ayuda, compartido por las dos escenas.</summary>
    public static class HelpContent
    {
        public const string Title = "Siempre hacia arriba";
        public const string HeadingHex = "#B8FA82";

        public const string Intro = "Escalá los 6 sectores hasta la meta. La lava empieza a subir después de 8 segundos y acelera con el tiempo.";

        public static readonly string[] Headings =
        {
            "CONTROLES",
            "TRANSFORMACIONES",
            "COLORES Y OBSTÁCULOS",
            "CHECKPOINTS Y CAÍDAS",
            "CARRERA ONLINE · 2 A 4 JUGADORES"
        };

        public static readonly string[] Bodies =
        {
            "WASD / Flechas: moverse según la cámara.\nEspacio: saltar; un salto por pulsación.\nQ: pequeño, más rápido y capaz de cruzar conductos.\nE: gigante; F empuja bloques cercanos.\nR: volver al tamaño normal.\nF: guardar checkpoint sobre su botón verde.\nRatón: arrastrar con botón izquierdo, derecho o central para girar cámara.\nRueda: acercar o alejar.\nC: mirar hacia la siguiente plataforma.\nEsc / P: abrir pausa; Enter: continuar en modo solo.",
            "Pequeño y gigante duran 7 segundos. Recargan durante 4 segundos al terminar. Pequeño corre un 35 % más rápido; gigante camina más lento y resiste mejor los golpes. Si un techo u otro jugador impiden crecer, seguís pequeño hasta tener espacio.",
            "Ámbar: plataformas frágiles; ceden 1,8 segundos después de pisarlas y reaparecen 5 segundos después.\nCian: plataformas móviles; calculá el salto y viajá sobre ellas.\nRojo: barras, martillos y prensas; evitá sus golpes.\nVioleta: bloques pesados; usá E y luego F para abrir paso.\nVerde: checkpoints y meta.",
            "Hay 3 bases exteriores junto al recorrido. Parate sobre el botón verde y pulsá F para guardar. Cada jugador guarda su propio avance durante esa ronda. Caer por debajo de la plataforma desde la que saliste o tocar lava te devuelve al último checkpoint, si todavía está por encima de la lava. Sin un checkpoint seguro, termina tu intento.",
            "Usen la misma versión del juego. En Internet, un jugador crea partida y comparte el código; los demás lo escriben y se unen, aunque estén en otras redes. El host debe mantener el juego abierto. Todos marcan Listo; el host inicia la carrera.\nLAN permite jugar en la misma red usando la IP local del host y puerto UDP 7777.\nCada jugador tiene un color: rosa, azul, verde o naranja. La barra derecha muestra J1–J4 según su altura, de INICIO a META; tu marcador lleva una raya blanca. Los eliminados quedan atenuados. El primero en llegar gana.\nAbrir pausa o ayuda no detiene la carrera online. Si el host sale, la partida se cierra para todos. No se puede entrar durante una carrera."
        };

        /// <summary>Arma el cuerpo completo con encabezados en color, listo para un TMP_Text.</summary>
        public static string BuildRichText()
        {
            StringBuilder builder = new StringBuilder(Intro.Length + 2048);
            builder.Append(Intro);
            for (int i = 0; i < Headings.Length; i++)
            {
                builder.Append("\n\n<b><color=").Append(HeadingHex).Append('>')
                    .Append(Headings[i]).Append("</color></b>\n");
                builder.Append(Bodies[i]);
            }
            return builder.ToString();
        }
    }
}
