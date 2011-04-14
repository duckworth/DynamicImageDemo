using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace Imaging
{
    public class ImageGenerator
    {
        public Bitmap GenerateDemoImage(string imageDirectoryPath, string iconName, string text)
        {
            var background = new Bitmap(imageDirectoryPath + "background.png");
            var icon = new Bitmap(imageDirectoryPath + iconName);
            var rect = new Rectangle(1, 1, background.Width, background.Height);
            var solidBrush = new SolidBrush(Color.WhiteSmoke);
            var font = new Font("Arial", 24, FontStyle.Bold, GraphicsUnit.Pixel);

            var stringFormat = new StringFormat
                                   {
                                       Alignment = StringAlignment.Center,
                                       LineAlignment = StringAlignment.Center
                                   };
            var g = Graphics.FromImage(background);
            g.TextRenderingHint = TextRenderingHint.AntiAlias;
            g.DrawString(text, font, solidBrush, rect, stringFormat);
            g.DrawImage(icon, 10, 12);
            g.Dispose();
            icon.Dispose();
            font.Dispose();

            return background;
        }
    }
}