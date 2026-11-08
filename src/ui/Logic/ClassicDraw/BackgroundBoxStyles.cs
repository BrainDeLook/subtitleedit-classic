using System;
using System.Text;
using Nikse.SubtitleEdit.Logic.AssistedEditing;
namespace Nikse.SubtitleEdit.Logic.ClassicDraw
{
public static class BackgroundBoxStyles
{
    public static readonly string[] Keys = new[] { "square", "rounded", "circle", "spikes", "bubbles", "wave", "hexagon", "torn-paper", "cloud", "torn-paper-double", "starburst", "scroll", "pill", "cut-corners", "slanted", "ticket", "speech-bubble", "ribbon", "brush-stroke", "corner-brackets", "underline", "frame", "arrow-tag", "side-bar" };
    public static string[] Names => ClassicAssistedText.L("ru", "en") == "ru" ? new[] { "Прямые углы", "Скруглённые углы", "Эллипс", "Шипы", "Пузырьки", "Волна", "Шестиугольник", "Рваная бумага", "Облако", "Рваные верх и низ", "Вспышка", "Свиток", "Капсула", "Срезанные углы", "Наклон", "Билет", "Облачко речи", "Лента", "Мазок кисти", "Уголки", "Подчёркивание", "Рамка", "Стрелка", "Боковая полоса" } : new[] { "Square corners", "Rounded corners", "Circle", "Spikes", "Bubbles", "Wave", "Hexagon", "Torn paper", "Cloud", "Torn paper (top and bottom)", "Starburst", "Scroll", "Pill", "Cut corners", "Slanted", "Ticket", "Speech bubble", "Ribbon", "Brush stroke", "Corner brackets", "Underline", "Frame", "Arrow tag", "Side bar" };
    public static string GenerateBackgroundBox(int boxStyleIndex, int radius, int left, int right, int top, int bottom)
    {
        var width = right - left;
        var height = bottom - top;

        if (boxStyleIndex == 1 && radius > 0) // Rounded corners
        {
            var r = Math.Min(radius, Math.Min(width / 2, height / 2));
            return
                $"{{\\p1}}m {left + r} {top} l {right - r} {top} b {right} {top} {right} {top + r} {right} {top + r} l {right} {bottom - r} b {right} {bottom} {right - r} {bottom} {right - r} {bottom} l {left + r} {bottom} b {left} {bottom} {left} {bottom - r} {left} {bottom - r} l {left} {top + r} b {left} {top} {left + r} {top} {left + r} {top}{{\\p0}}";
        }
        else if (boxStyleIndex == 2) // Circle
        {
            var centerX = (left + right) / 2;
            var centerY = (top + bottom) / 2;
            var radiusX = width / 2;
            var radiusY = height / 2;

            // Simplified circle approximation using bezier curves
            var kappa = 0.5522847498;
            var ox = (int)(radiusX * kappa);
            var oy = (int)(radiusY * kappa);

            return
                $"{{\\p1}}m {centerX} {centerY - radiusY} b {centerX + ox} {centerY - radiusY} {centerX + radiusX} {centerY - oy} {centerX + radiusX} {centerY} b {centerX + radiusX} {centerY + oy} {centerX + ox} {centerY + radiusY} {centerX} {centerY + radiusY} b {centerX - ox} {centerY + radiusY} {centerX - radiusX} {centerY + oy} {centerX - radiusX} {centerY} b {centerX - radiusX} {centerY - oy} {centerX - ox} {centerY - radiusY} {centerX} {centerY - radiusY}{{\\p0}}";
        }
        else if (boxStyleIndex == 3) // Spikes
        {
            return GenerateSpikesBox(left, right, top, bottom);
        }
        else if (boxStyleIndex == 4) // Bubbles
        {
            return GenerateBubblesBox(left, right, top, bottom);
        }

        else if (boxStyleIndex == 5) // Wave
        {
            return GenerateWaveBox(left, right, top, bottom);
        }
        else if (boxStyleIndex == 6) // Hexagon
        {
            return GenerateHexagonBox(left, right, top, bottom);
        }
        else if (boxStyleIndex == 7) // Torn paper
        {
            return GenerateTornPaperBox(left, right, top, bottom);
        }
        else if (boxStyleIndex == 8) // Cloud
        {
            return GenerateCloudBox(left, right, top, bottom);
        }
        else if (boxStyleIndex == 9) // Torn paper (top & bottom)
        {
            return GenerateTornPaperDoubleBox(left, right, top, bottom);
        }
        else if (boxStyleIndex == 10) // Starburst
        {
            return GenerateStarburstBox(left, right, top, bottom);
        }
        else if (boxStyleIndex == 11) // Scroll / Parchment
        {
            return GenerateScrollBox(left, right, top, bottom);
        }
        else if (boxStyleIndex == 12) // Pill: fully round ends
        {
            return GenerateBackgroundBox(1, Math.Max(1, height / 2), left, right, top, bottom);
        }
        else if (boxStyleIndex == 13)
        {
            return GenerateCutCornersBox(left, right, top, bottom);
        }
        else if (boxStyleIndex == 14)
        {
            return GenerateSlantedBox(left, right, top, bottom);
        }
        else if (boxStyleIndex == 15)
        {
            return GenerateTicketBox(left, right, top, bottom);
        }
        else if (boxStyleIndex == 16)
        {
            return GenerateSpeechBubbleBox(left, right, top, bottom);
        }
        else if (boxStyleIndex == 17)
        {
            return GenerateRibbonBox(left, right, top, bottom);
        }
        else if (boxStyleIndex == 18)
        {
            return GenerateBrushStrokeBox(left, right, top, bottom);
        }
        else if (boxStyleIndex == 19)
        {
            return GenerateCornerBracketsBox(left, right, top, bottom);
        }
        else if (boxStyleIndex == 20)
        {
            return GenerateUnderlineBox(left, right, top, bottom);
        }
        else if (boxStyleIndex == 21)
        {
            return GenerateFrameBox(left, right, top, bottom);
        }
        else if (boxStyleIndex == 22)
        {
            return GenerateArrowTagBox(left, right, top, bottom);
        }
        else if (boxStyleIndex == 23)
        {
            return GenerateSideBarBox(left, right, top, bottom);
        }

        // Square corners (default)
        return $"{{\\p1}}m {left} {top} l {right} {top} {right} {bottom} {left} {bottom}{{\\p0}}";
    }

    private static string GenerateCutCornersBox(int left, int right, int top, int bottom)
    {
        var c = Math.Max(2, Math.Min((bottom - top) / 3, (right - left) / 4));
        return $"{{\\p1}}m {left + c} {top} l {right - c} {top} {right} {top + c} {right} {bottom - c} {right - c} {bottom} {left + c} {bottom} {left} {bottom - c} {left} {top + c}{{\\p0}}";
    }

    /// <summary>
    /// A parallelogram leaning right, wide enough that the text stays inside.
    /// </summary>
    private static string GenerateSlantedBox(int left, int right, int top, int bottom)
    {
        var s = Math.Max(2, (bottom - top) * 35 / 100);
        return $"{{\\p1}}m {left} {top} l {right + s} {top} {right} {bottom} {left - s} {bottom}{{\\p0}}";
    }

    /// <summary>
    /// A ticket: half-round notches cut into the middle of both ends.
    /// </summary>
    private static string GenerateTicketBox(int left, int right, int top, int bottom)
    {
        var cy = (top + bottom) / 2;
        var n = Math.Max(2, (bottom - top) / 4);
        var k = (int)Math.Round(n * 1.333);
        return $"{{\\p1}}m {left} {top} l {right} {top} {right} {cy - n} b {right - k} {cy - n} {right - k} {cy + n} {right} {cy + n} " +
               $"l {right} {bottom} {left} {bottom} {left} {cy + n} b {left + k} {cy + n} {left + k} {cy - n} {left} {cy - n}{{\\p0}}";
    }

    /// <summary>
    /// A rounded box with a tail under its left part, pointing down at the speaker.
    /// </summary>
    private static string GenerateSpeechBubbleBox(int left, int right, int top, int bottom)
    {
        var width = right - left;
        var height = bottom - top;
        var r = Math.Max(1, Math.Min(18, Math.Min(width / 4, height / 3)));
        var tailLeft = left + width / 5;
        var tailRight = tailLeft + Math.Max(8, width / 10);
        var tipX = tailLeft - Math.Max(4, width / 30);
        var tipY = bottom + Math.Max(10, height * 45 / 100);
        return $"{{\\p1}}m {left + r} {top} l {right - r} {top} b {right} {top} {right} {top} {right} {top + r} " +
               $"l {right} {bottom - r} b {right} {bottom} {right} {bottom} {right - r} {bottom} " +
               $"l {tailRight} {bottom} {tipX} {tipY} {tailLeft} {bottom} {left + r} {bottom} b {left} {bottom} {left} {bottom} {left} {bottom - r} " +
               $"l {left} {top + r} b {left} {top} {left} {top} {left + r} {top}{{\\p0}}";
    }

    /// <summary>
    /// A banner with V-cut ends, reaching out past the text on both sides.
    /// </summary>
    private static string GenerateRibbonBox(int left, int right, int top, int bottom)
    {
        var d = Math.Max(4, (bottom - top) * 40 / 100);
        var cy = (top + bottom) / 2;
        return $"{{\\p1}}m {left - d} {top} l {right + d} {top} {right} {cy} {right + d} {bottom} {left - d} {bottom} {left} {cy}{{\\p0}}";
    }

    /// <summary>
    /// A rough paint stroke: uneven top and bottom edges and ragged ends. The roughness is seeded
    /// by the box size, so the preview doesn't jitter and lines of equal size match.
    /// </summary>
    private static string GenerateBrushStrokeBox(int left, int right, int top, int bottom)
    {
        var width = right - left;
        var height = bottom - top;
        var random = new Random(width * 7919 + height);
        var amplitude = Math.Max(2, height / 12);
        var step = Math.Max(8, height / 3);
        var sb = new StringBuilder();
        sb.Append($"m {left} {top + random.Next(0, amplitude)} l ");
        for (var x = left + step; x < right; x += step)
        {
            sb.Append($"{x} {top + random.Next(-amplitude, amplitude + 1)} ");
        }

        // Ragged right end
        for (var i = 0; i <= 3; i++)
        {
            sb.Append($"{right + random.Next(-amplitude, amplitude * 2 + 1)} {top + height * i / 3} ");
        }

        for (var x = right - step; x > left; x -= step)
        {
            sb.Append($"{x} {bottom + random.Next(-amplitude, amplitude + 1)} ");
        }

        // Ragged left end
        for (var i = 3; i >= 1; i--)
        {
            sb.Append($"{left - random.Next(-amplitude, amplitude * 2 + 1)} {top + height * i / 3} ");
        }

        return $"{{\\p1}}{sb.ToString().TrimEnd()}{{\\p0}}";
    }

    /// <summary>
    /// Four L-shaped corner marks framing the text, no fill in between.
    /// </summary>
    private static string GenerateCornerBracketsBox(int left, int right, int top, int bottom)
    {
        var t = Math.Max(3, (bottom - top) / 12);
        var a = Math.Max(t * 2, Math.Min(right - left, bottom - top) / 3);
        return "{\\p1}" +
               $"m {left} {top} l {left + a} {top} {left + a} {top + t} {left + t} {top + t} {left + t} {top + a} {left} {top + a} " +
               $"m {right} {top} l {right} {top + a} {right - t} {top + a} {right - t} {top + t} {right - a} {top + t} {right - a} {top} " +
               $"m {right} {bottom} l {right - a} {bottom} {right - a} {bottom - t} {right - t} {bottom - t} {right - t} {bottom - a} {right} {bottom - a} " +
               $"m {left} {bottom} l {left} {bottom - a} {left + t} {bottom - a} {left + t} {bottom - t} {left + a} {bottom - t} {left + a} {bottom}" +
               "{\\p0}";
    }

    /// <summary>
    /// Just a bar along the bottom of the padded box, under the text.
    /// </summary>
    private static string GenerateUnderlineBox(int left, int right, int top, int bottom)
    {
        var t = Math.Max(4, (bottom - top) / 10);
        return $"{{\\p1}}m {left} {bottom - t} l {right} {bottom - t} {right} {bottom} {left} {bottom}{{\\p0}}";
    }

    /// <summary>
    /// Only the border: the inner rectangle is wound the other way, so it is a hole.
    /// </summary>
    private static string GenerateFrameBox(int left, int right, int top, int bottom)
    {
        var t = Math.Max(3, (bottom - top) / 10);
        return $"{{\\p1}}m {left} {top} l {right} {top} {right} {bottom} {left} {bottom} " +
               $"m {left + t} {top + t} l {left + t} {bottom - t} {right - t} {bottom - t} {right - t} {top + t}{{\\p0}}";
    }

    /// <summary>
    /// A label tag: square on the left, pointed on the right.
    /// </summary>
    private static string GenerateArrowTagBox(int left, int right, int top, int bottom)
    {
        var d = Math.Max(4, (bottom - top) / 2);
        var cy = (top + bottom) / 2;
        return $"{{\\p1}}m {left} {top} l {right} {top} {right + d} {cy} {right} {bottom} {left} {bottom}{{\\p0}}";
    }

    /// <summary>
    /// A vertical accent bar at the left of the padded box, lower-third style.
    /// </summary>
    private static string GenerateSideBarBox(int left, int right, int top, int bottom)
    {
        var t = Math.Max(4, (bottom - top) / 8);
        return $"{{\\p1}}m {left} {top} l {left + t} {top} {left + t} {bottom} {left} {bottom}{{\\p0}}";
    }

    private static string GenerateWaveBox(int left, int right, int top, int bottom)
    {
        const int amplitude = 6;
        var halfWaves = Math.Max(4, Math.Min(12, (right - left) / 30));
        var segW = (right - left) / (double)halfWaves;

        var sb = new StringBuilder();
        sb.Append($"m {left} {top} ");

        // Top edge: alternating up/down bezier bumps (left to right)
        for (var i = 0; i < halfWaves; i++)
        {
            var x = left + i * segW;
            var xEnd = left + (i + 1) * segW;
            var yCtrl = i % 2 == 0 ? top - amplitude : top + amplitude;
            sb.Append($"b {(int)(x + segW / 3)} {yCtrl} {(int)(x + 2 * segW / 3)} {yCtrl} {(int)xEnd} {top} ");
        }

        // Right edge
        sb.Append($"l {right} {bottom} ");

        // Bottom edge: mirror wave pattern (right to left)
        for (var i = halfWaves - 1; i >= 0; i--)
        {
            var x = left + i * segW;
            var yCtrl = i % 2 == 0 ? bottom + amplitude : bottom - amplitude;
            sb.Append($"b {(int)(x + 2 * segW / 3)} {yCtrl} {(int)(x + segW / 3)} {yCtrl} {(int)x} {bottom} ");
        }

        return $"{{\\p1}}{sb.ToString().TrimEnd()}{{\\p0}}";
    }

    private static string GenerateHexagonBox(int left, int right, int top, int bottom)
    {
        var cy = (top + bottom) / 2;
        var chamfer = Math.Min((bottom - top) / 2, (right - left) / 5);
        return $"{{\\p1}}m {left + chamfer} {top} l {right - chamfer} {top} {right} {cy} {right - chamfer} {bottom} {left + chamfer} {bottom} {left} {cy}{{\\p0}}";
    }

    private static string GenerateTornPaperBox(int left, int right, int top, int bottom)
    {
        var rng = new Random((left * 397) ^ (top * 613) ^ right);
        const int maxTearH = 22;
        var count = Math.Max(3, (right - left) / 28);
        var segW = (right - left) / (double)count;
        var sb = new StringBuilder();
        sb.Append($"m {left} {top} ");
        for (var i = 0; i < count; i++)
        {
            var tipX = (int)(left + i * segW + segW * (0.2 + rng.NextDouble() * 0.6));
            var tipY = top - rng.Next(8, maxTearH + 1);
            var endX = i == count - 1 ? right : (int)(left + (i + 1) * segW);
            sb.Append($"l {tipX} {tipY} {endX} {top} ");
        }
        sb.Append($"l {right} {bottom} {left} {bottom}");
        return $"{{\\p1}}{sb.ToString().TrimEnd()}{{\\p0}}";
    }

    private static string GenerateCloudBox(int left, int right, int top, int bottom)
    {
        const double k = 0.5523;
        var w = right - left;
        var h = bottom - top;

        // Single target radius keeps all bumps roughly the same size on every edge.
        // Clamp so tiny boxes still look cloud-like and large boxes don't get huge bumps.
        var targetR = DrawMath.Clamp(Math.Min(w, h) / 4.0, 14.0, 48.0);

        // Snap each edge's count to the nearest integer, then recompute the exact radius
        // so bumps fill the edge perfectly with no gaps.
        var hCount = Math.Max(2, (int)Math.Round(w / (2.0 * targetR)));
        var hR = w / (2.0 * hCount);

        var vCount = Math.Max(1, (int)Math.Round(h / (2.0 * targetR)));
        var vR = h / (2.0 * vCount);

        var sb = new StringBuilder();
        var curX = (double)left;
        var curY = (double)top;
        sb.Append($"m {left} {top} ");

        // Top edge: bell-curved bump heights � taller in the middle, shorter at the ends
        for (var i = 0; i < hCount; i++)
        {
            var t = hCount > 1 ? (double)i / (hCount - 1) : 0.5;
            var bell = 1.0 - 4.0 * (t - 0.5) * (t - 0.5); // 1 at centre, 0 at edges
            var bumpH = hR * (0.6 + 0.9 * bell);            // 0.6�hR at edges ? 1.5�hR at centre
            sb.Append($"b {(int)curX} {(int)(top - bumpH * k)} {(int)(curX + hR * (1 - k))} {(int)(top - bumpH)} {(int)(curX + hR)} {(int)(top - bumpH)} ");
            sb.Append($"b {(int)(curX + hR * (1 + k))} {(int)(top - bumpH)} {(int)(curX + 2 * hR)} {(int)(top - bumpH * k)} {(int)(curX + 2 * hR)} {top} ");
            curX += 2 * hR;
        }

        // Right edge: uniform bumps rightward (top ? bottom)
        curY = top;
        for (var i = 0; i < vCount; i++)
        {
            sb.Append($"b {(int)(right + vR * k)} {(int)curY} {(int)(right + vR)} {(int)(curY + vR * (1 - k))} {(int)(right + vR)} {(int)(curY + vR)} ");
            sb.Append($"b {(int)(right + vR)} {(int)(curY + vR * (1 + k))} {(int)(right + vR * k)} {(int)(curY + 2 * vR)} {right} {(int)(curY + 2 * vR)} ");
            curY += 2 * vR;
        }

        // Bottom edge: uniform bumps downward (right ? left)
        curX = right;
        for (var i = 0; i < hCount; i++)
        {
            sb.Append($"b {(int)curX} {(int)(bottom + hR * k)} {(int)(curX - hR * (1 - k))} {(int)(bottom + hR)} {(int)(curX - hR)} {(int)(bottom + hR)} ");
            sb.Append($"b {(int)(curX - hR * (1 + k))} {(int)(bottom + hR)} {(int)(curX - 2 * hR)} {(int)(bottom + hR * k)} {(int)(curX - 2 * hR)} {bottom} ");
            curX -= 2 * hR;
        }

        // Left edge: uniform bumps leftward (bottom ? top)
        curY = bottom;
        for (var i = 0; i < vCount; i++)
        {
            sb.Append($"b {(int)(left - vR * k)} {(int)curY} {(int)(left - vR)} {(int)(curY - vR * (1 - k))} {(int)(left - vR)} {(int)(curY - vR)} ");
            sb.Append($"b {(int)(left - vR)} {(int)(curY - vR * (1 + k))} {(int)(left - vR * k)} {(int)(curY - 2 * vR)} {left} {(int)(curY - 2 * vR)} ");
            curY -= 2 * vR;
        }

        return $"{{\\p1}}{sb.ToString().TrimEnd()}{{\\p0}}";
    }

    private static string GenerateTornPaperDoubleBox(int left, int right, int top, int bottom)
    {
        var rng = new Random((left * 397) ^ (top * 613) ^ right);
        const int maxTearH = 22;
        var count = Math.Max(3, (right - left) / 28);
        var segW = (right - left) / (double)count;
        var sb = new StringBuilder();
        sb.Append($"m {left} {top} ");
        for (var i = 0; i < count; i++)
        {
            var tipX = (int)(left + i * segW + segW * (0.2 + rng.NextDouble() * 0.6));
            var tipY = top - rng.Next(8, maxTearH + 1);
            var endX = i == count - 1 ? right : (int)(left + (i + 1) * segW);
            sb.Append($"l {tipX} {tipY} {endX} {top} ");
        }
        sb.Append($"l {right} {bottom} ");
        for (var i = count - 1; i >= 0; i--)
        {
            var tipX = (int)(left + i * segW + segW * (0.2 + rng.NextDouble() * 0.6));
            var tipY = bottom + rng.Next(8, maxTearH + 1);
            var endX = i == 0 ? left : (int)(left + i * segW);
            sb.Append($"l {tipX} {tipY} {endX} {bottom} ");
        }
        return $"{{\\p1}}{sb.ToString().TrimEnd()}{{\\p0}}";
    }

    private static string GenerateSpikesBox(int left, int right, int top, int bottom)
    {
        const int spikeH = 8;
        var count = Math.Max(2, (right - left) / 20);
        var spikeW = (right - left) / (double)count;

        var sb = new StringBuilder();
        sb.Append($"m {left} {top} ");

        // Top edge: spikes pointing up (left to right)
        for (var i = 0; i < count; i++)
        {
            var tipX = (int)(left + i * spikeW + spikeW / 2.0);
            var endX = (int)(left + (i + 1) * spikeW);
            sb.Append($"l {tipX} {top - spikeH} {endX} {top} ");
        }

        // Right edge
        sb.Append($"l {right} {bottom} ");

        // Bottom edge: spikes pointing down (right to left)
        for (var i = count - 1; i >= 0; i--)
        {
            var tipX = (int)(left + i * spikeW + spikeW / 2.0);
            var endX = (int)(left + i * spikeW);
            sb.Append($"l {tipX} {bottom + spikeH} {endX} {bottom} ");
        }

        return $"{{\\p1}}{sb.ToString().TrimEnd()}{{\\p0}}";
    }

    private static string GenerateBubblesBox(int left, int right, int top, int bottom)
    {
        const double k = 0.5523;
        var count = Math.Max(2, (right - left) / 18);
        var d = (right - left) / (double)count;
        var r = d / 2.0;

        var sb = new StringBuilder();
        sb.Append($"m {left} {top} ");

        // Top edge: bubbles pointing up (left to right)
        for (var i = 0; i < count; i++)
        {
            var bx = left + i * d;
            sb.Append($"b {(int)bx} {(int)(top - r * k)} {(int)(bx + r * (1 - k))} {(int)(top - r)} {(int)(bx + r)} {(int)(top - r)} ");
            sb.Append($"b {(int)(bx + r * (1 + k))} {(int)(top - r)} {(int)(bx + 2 * r)} {(int)(top - r * k)} {(int)(bx + 2 * r)} {top} ");
        }

        // Right edge
        sb.Append($"l {right} {bottom} ");

        // Bottom edge: bubbles pointing down (right to left)
        for (var i = count - 1; i >= 0; i--)
        {
            var bx = left + i * d;
            sb.Append($"b {(int)(bx + 2 * r)} {(int)(bottom + r * k)} {(int)(bx + r * (1 + k))} {(int)(bottom + r)} {(int)(bx + r)} {(int)(bottom + r)} ");
            sb.Append($"b {(int)(bx + r * (1 - k))} {(int)(bottom + r)} {(int)bx} {(int)(bottom + r * k)} {(int)bx} {bottom} ");
        }

        return $"{{\\p1}}{sb.ToString().TrimEnd()}{{\\p0}}";
    }

    private static string GenerateStarburstBox(int left, int right, int top, int bottom)
    {
        var cx = (left + right) / 2.0;
        var cy = (top + bottom) / 2.0;
        var a = (right - left) / 2.0;
        var b = (bottom - top) / 2.0;

        var baseSpikeOut = DrawMath.Clamp(Math.Min(a, b) * 0.55, 8.0, 28.0);
        var rng = new Random((left * 397) ^ (top * 613) ^ right);

        // Random spike count per subtitle: 9�20 spikes
        var numSpikes = rng.Next(9, 21);
        var numPoints = numSpikes * 2;

        var sb = new StringBuilder();
        for (var i = 0; i < numPoints; i++)
        {
            var angle = i * 2.0 * Math.PI / numPoints - Math.PI / 2.0;
            var cos = Math.Cos(angle);
            var sin = Math.Sin(angle);
            var ellipseR = (a * b) / Math.Sqrt(b * b * cos * cos + a * a * sin * sin);
            double r;
            if (i % 2 == 0) // outer spike: very wide random range
            {
                r = ellipseR + baseSpikeOut * (0.1 + rng.NextDouble() * 2.4);
            }
            else // inner valley: wide random depth
            {
                r = ellipseR * (0.50 + rng.NextDouble() * 0.40);
            }
            var px = (int)(cx + r * cos);
            var py = (int)(cy + r * sin);
            sb.Append(i == 0 ? $"m {px} {py} " : $"l {px} {py} ");
        }

        return $"{{\\p1}}{sb.ToString().TrimEnd()}{{\\p0}}";
    }

    private static string GenerateScrollBox(int left, int right, int top, int bottom)
    {
        var h = bottom - top;
        var curve = Math.Max(10, Math.Min(30, h / 2));
        var q = h / 4;

        // Left and right edges are S-curves (bezier), top and bottom are straight lines
        return $"{{\\p1}}m {left} {top} " +
               $"l {right} {top} " +
               $"b {right + curve} {top + q} {right - curve} {bottom - q} {right} {bottom} " +
               $"l {left} {bottom} " +
               $"b {left - curve} {bottom - q} {left + curve} {top + q} {left} {top}{{\\p0}}";
    }

}
}
