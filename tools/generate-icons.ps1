using namespace System.Drawing
using namespace System.Drawing.Drawing2D
using namespace System.Drawing.Imaging

Add-Type -AssemblyName System.Drawing

$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent $PSScriptRoot
$size = 256

function New-Canvas {
    param([int]$CanvasSize)

    $bitmap = [Bitmap]::new($CanvasSize, $CanvasSize)
    $graphics = [Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [SmoothingMode]::AntiAlias
    $graphics.InterpolationMode = [InterpolationMode]::HighQualityBicubic
    $graphics.PixelOffsetMode = [PixelOffsetMode]::HighQuality
    $graphics.CompositingQuality = [CompositingQuality]::HighQuality

    return @{
        Bitmap = $bitmap
        Graphics = $graphics
    }
}

function Save-Canvas {
    param(
        [hashtable]$Canvas,
        [string]$Path
    )

    $Canvas.Bitmap.Save($Path, [ImageFormat]::Png)
    $Canvas.Graphics.Dispose()
    $Canvas.Bitmap.Dispose()
}

function Fill-BaseSky {
    param(
        [Graphics]$Graphics,
        [int]$CanvasSize,
        [Color]$TopColor,
        [Color]$BottomColor
    )

    $rect = [RectangleF]::new(0, 0, $CanvasSize, $CanvasSize)
    $brush = [LinearGradientBrush]::new($rect, $TopColor, $BottomColor, 90)
    $Graphics.FillRectangle($brush, $rect)
    $brush.Dispose()
}

function Add-AtmosphereStrokes {
    param(
        [Graphics]$Graphics,
        [int]$CanvasSize,
        [Color]$StrokeColor
    )

    $strokes = @(
        @{ X1 = -10; Y1 = 28; X2 = 180; Y2 = 72; Width = 16 },
        @{ X1 = 64; Y1 = 14; X2 = 246; Y2 = 60; Width = 10 },
        @{ X1 = -16; Y1 = 86; X2 = 138; Y2 = 124; Width = 14 }
    )

    foreach ($stroke in $strokes) {
        $pen = [Pen]::new($StrokeColor, $stroke.Width)
        $pen.StartCap = [LineCap]::Round
        $pen.EndCap = [LineCap]::Round
        $Graphics.DrawLine($pen, $stroke.X1, $stroke.Y1, $stroke.X2, $stroke.Y2)
        $pen.Dispose()
    }

    $mistBrush = [SolidBrush]::new([Color]::FromArgb(20, 255, 255, 255))
    $Graphics.FillEllipse($mistBrush, -20, 18, 220, 84)
    $Graphics.FillEllipse($mistBrush, 90, 10, 170, 60)
    $mistBrush.Dispose()
}

function Draw-FogBank {
    param(
        [Graphics]$Graphics,
        [int]$CanvasSize,
        [int]$YOffset,
        [double]$Scale = 1.0
    )

    $fogColors = @(
        [Color]::FromArgb(66, 187, 235, 175),
        [Color]::FromArgb(58, 143, 214, 147),
        [Color]::FromArgb(40, 208, 229, 210),
        [Color]::FromArgb(34, 136, 183, 172)
    )

    $fogData = @(
        @{ X = -22; Y = 182; W = 110; H = 62; ColorIndex = 0 },
        @{ X = 22; Y = 190; W = 96; H = 54; ColorIndex = 1 },
        @{ X = 76; Y = 184; W = 120; H = 68; ColorIndex = 2 },
        @{ X = 130; Y = 192; W = 112; H = 60; ColorIndex = 0 },
        @{ X = 178; Y = 186; W = 102; H = 58; ColorIndex = 1 },
        @{ X = -8; Y = 206; W = 124; H = 60; ColorIndex = 3 },
        @{ X = 94; Y = 208; W = 134; H = 64; ColorIndex = 2 },
        @{ X = 180; Y = 206; W = 104; H = 58; ColorIndex = 0 }
    )

    foreach ($cloud in $fogData) {
        $brush = [SolidBrush]::new($fogColors[$cloud.ColorIndex])
        $Graphics.FillEllipse(
            $brush,
            [single]($cloud.X * $Scale),
            [single](($cloud.Y + $YOffset) * $Scale),
            [single]($cloud.W * $Scale),
            [single]($cloud.H * $Scale)
        )
        $brush.Dispose()
    }

    $haze = [SolidBrush]::new([Color]::FromArgb(24, 230, 245, 235))
    $Graphics.FillRectangle($haze, 0, [single](170 * $Scale + $YOffset), $CanvasSize, [single](70 * $Scale))
    $haze.Dispose()
}

function New-PolygonPath {
    param([PointF[]]$Points)

    $path = [GraphicsPath]::new()
    $path.AddPolygon($Points)
    return $path
}

function Draw-Mountain {
    param(
        [Graphics]$Graphics,
        [PointF[]]$Points,
        [Color]$FillColor,
        [Color]$RimColor
    )

    $path = New-PolygonPath -Points $Points
    $brush = [SolidBrush]::new($FillColor)
    $Graphics.FillPath($brush, $path)
    $brush.Dispose()

    $pen = [Pen]::new($RimColor, 4)
    $pen.LineJoin = [LineJoin]::Round
    $Graphics.DrawPath($pen, $path)
    $pen.Dispose()
    $path.Dispose()
}

function Draw-RidgeHighlight {
    param(
        [Graphics]$Graphics,
        [PointF[]]$Points
    )

    $pen = [Pen]::new([Color]::FromArgb(90, 158, 176, 190), 4)
    $pen.StartCap = [LineCap]::Round
    $pen.EndCap = [LineCap]::Round
    $pen.LineJoin = [LineJoin]::Round
    $Graphics.DrawLines($pen, $Points)
    $pen.Dispose()
}

function Draw-Beacon {
    param(
        [Graphics]$Graphics,
        [single]$X,
        [single]$BaseY,
        [single]$BeamHeight,
        [single]$BeamWidth
    )

    $glowBrush = [SolidBrush]::new([Color]::FromArgb(46, 255, 218, 117))
    $Graphics.FillEllipse($glowBrush, $X - 30, $BaseY - 14, 60, 28)
    $Graphics.FillEllipse($glowBrush, $X - 18, $BaseY - 42, 36, 28)
    $glowBrush.Dispose()

    for ($i = 4; $i -ge 0; $i--) {
        $alpha = 26 + ($i * 10)
        $extraWidth = $i * 10
        $beamBrush = [SolidBrush]::new([Color]::FromArgb($alpha, 255, 221, 126))
        $Graphics.FillRectangle(
            $beamBrush,
            $X - ($BeamWidth / 2) - $extraWidth,
            $BaseY - $BeamHeight,
            $BeamWidth + ($extraWidth * 2),
            $BeamHeight
        )
        $beamBrush.Dispose()
    }

    $coreBrush = [SolidBrush]::new([Color]::FromArgb(150, 255, 235, 171))
    $Graphics.FillRectangle($coreBrush, $X - ($BeamWidth / 2), $BaseY - $BeamHeight, $BeamWidth, $BeamHeight)
    $coreBrush.Dispose()

    $outerRing = [Pen]::new([Color]::FromArgb(180, 255, 189, 90), 6)
    $innerRing = [SolidBrush]::new([Color]::FromArgb(255, 255, 126, 76))
    $Graphics.DrawEllipse($outerRing, $X - 12, $BaseY - 8, 24, 24)
    $Graphics.FillEllipse($innerRing, $X - 8, $BaseY - 4, 16, 16)
    $outerRing.Dispose()
    $innerRing.Dispose()
}

function Draw-Compass {
    param(
        [Graphics]$Graphics,
        [single]$X,
        [single]$Y,
        [single]$Radius,
        [single]$RotationDegrees = 0
    )

    $state = $Graphics.Save()
    $Graphics.TranslateTransform($X, $Y)
    $Graphics.RotateTransform($RotationDegrees)

    $shadowBrush = [SolidBrush]::new([Color]::FromArgb(44, 0, 0, 0))
    $Graphics.FillEllipse($shadowBrush, -$Radius + 4, -$Radius + 6, $Radius * 2, $Radius * 2)
    $shadowBrush.Dispose()

    $faceBrush = [SolidBrush]::new([Color]::FromArgb(238, 22, 38, 50))
    $ringPen = [Pen]::new([Color]::FromArgb(255, 245, 206, 108), 5)
    $innerPen = [Pen]::new([Color]::FromArgb(120, 255, 237, 187), 2)
    $needlePen = [Pen]::new([Color]::FromArgb(255, 255, 210, 104), 3)
    $northPen = [Pen]::new([Color]::FromArgb(255, 245, 114, 84), 4)
    $centerBrush = [SolidBrush]::new([Color]::FromArgb(255, 241, 236, 220))
    $highlightBrush = [SolidBrush]::new([Color]::FromArgb(34, 255, 255, 255))

    $Graphics.FillEllipse($faceBrush, -$Radius, -$Radius, $Radius * 2, $Radius * 2)
    $Graphics.DrawEllipse($ringPen, -$Radius, -$Radius, $Radius * 2, $Radius * 2)
    $Graphics.DrawEllipse($innerPen, -($Radius - 7), -($Radius - 7), ($Radius - 7) * 2, ($Radius - 7) * 2)
    $Graphics.FillEllipse($highlightBrush, -$Radius + 6, -$Radius + 6, $Radius * 1.2, $Radius * 0.7)
    $Graphics.DrawLine($needlePen, 0, 0, -8, 14)
    $Graphics.DrawLine($northPen, 0, 0, 12, -18)
    $Graphics.FillEllipse($centerBrush, -4, -4, 8, 8)

    $faceBrush.Dispose()
    $ringPen.Dispose()
    $innerPen.Dispose()
    $needlePen.Dispose()
    $northPen.Dispose()
    $centerBrush.Dispose()
    $highlightBrush.Dispose()
    $Graphics.Restore($state)
}

function Draw-Climber {
    param(
        [Graphics]$Graphics,
        [single]$X,
        [single]$Y,
        [single]$Scale,
        [single]$TiltDegrees,
        [Color]$PrimaryColor,
        [Color]$AccentColor
    )

    $state = $Graphics.Save()
    $Graphics.TranslateTransform($X, $Y)
    $Graphics.RotateTransform($TiltDegrees)

    $limbPen = [Pen]::new($AccentColor, [single](4 * $Scale))
    $limbPen.StartCap = [LineCap]::Round
    $limbPen.EndCap = [LineCap]::Round
    $limbPen.LineJoin = [LineJoin]::Round

    $bodyBrush = [SolidBrush]::new($PrimaryColor)
    $headBrush = [SolidBrush]::new([Color]::FromArgb(255, 243, 214, 178))
    $packBrush = [SolidBrush]::new([Color]::FromArgb(255, 58, 77, 88))
    $shoePen = [Pen]::new([Color]::FromArgb(255, 35, 44, 52), [single](3 * $Scale))
    $shoePen.StartCap = [LineCap]::Round
    $shoePen.EndCap = [LineCap]::Round

    $Graphics.FillEllipse($headBrush, [single](-7 * $Scale), [single](-30 * $Scale), [single](14 * $Scale), [single](14 * $Scale))
    $Graphics.FillEllipse($packBrush, [single](-11 * $Scale), [single](-16 * $Scale), [single](12 * $Scale), [single](18 * $Scale))
    $Graphics.FillEllipse($bodyBrush, [single](-6 * $Scale), [single](-18 * $Scale), [single](16 * $Scale), [single](22 * $Scale))

    $Graphics.DrawLine($limbPen, [single](8 * $Scale), [single](-12 * $Scale), [single](20 * $Scale), [single](-24 * $Scale))
    $Graphics.DrawLine($limbPen, [single](4 * $Scale), [single](0 * $Scale), [single](18 * $Scale), [single](10 * $Scale))
    $Graphics.DrawLine($limbPen, [single](-2 * $Scale), [single](4 * $Scale), [single](-14 * $Scale), [single](18 * $Scale))
    $Graphics.DrawLine($shoePen, [single](18 * $Scale), [single](10 * $Scale), [single](23 * $Scale), [single](12 * $Scale))
    $Graphics.DrawLine($shoePen, [single](-14 * $Scale), [single](18 * $Scale), [single](-18 * $Scale), [single](22 * $Scale))

    $limbPen.Dispose()
    $bodyBrush.Dispose()
    $headBrush.Dispose()
    $packBrush.Dispose()
    $shoePen.Dispose()
    $Graphics.Restore($state)
}

function Draw-GlowDots {
    param(
        [Graphics]$Graphics,
        [PointF[]]$Points,
        [single]$Radius,
        [Color]$Color
    )

    foreach ($point in $Points) {
        $brush = [SolidBrush]::new($Color)
        $Graphics.FillEllipse($brush, $point.X - $Radius, $point.Y - $Radius, $Radius * 2, $Radius * 2)
        $brush.Dispose()
    }
}

function Draw-FogRibbon {
    param(
        [Graphics]$Graphics,
        [PointF[]]$Points,
        [Color]$Color,
        [single]$Width
    )

    $pen = [Pen]::new($Color, $Width)
    $pen.StartCap = [LineCap]::Round
    $pen.EndCap = [LineCap]::Round
    $pen.LineJoin = [LineJoin]::Round
    $Graphics.DrawCurve($pen, $Points)
    $pen.Dispose()
}

function Draw-OptionOne {
    param(
        [Graphics]$Graphics,
        [int]$CanvasSize
    )

    Fill-BaseSky -Graphics $Graphics -CanvasSize $CanvasSize -TopColor ([Color]::FromArgb(255, 19, 33, 47)) -BottomColor ([Color]::FromArgb(255, 95, 112, 120))
    Add-AtmosphereStrokes -Graphics $Graphics -CanvasSize $CanvasSize -StrokeColor ([Color]::FromArgb(34, 220, 228, 235))
    Draw-FogBank -Graphics $Graphics -CanvasSize $CanvasSize -YOffset 10
    Draw-FogRibbon -Graphics $Graphics -Points @(
        [PointF]::new(-12, 84),
        [PointF]::new(48, 58),
        [PointF]::new(130, 92),
        [PointF]::new(226, 76)
    ) -Color ([Color]::FromArgb(44, 242, 246, 248)) -Width 18

    $mainMountain = @(
        [PointF]::new(18, 214),
        [PointF]::new(122, 62),
        [PointF]::new(240, 214)
    )
    Draw-Mountain -Graphics $Graphics -Points $mainMountain -FillColor ([Color]::FromArgb(255, 17, 24, 29)) -RimColor ([Color]::FromArgb(255, 58, 77, 89))
    Draw-RidgeHighlight -Graphics $Graphics -Points @(
        [PointF]::new(98, 108),
        [PointF]::new(122, 68),
        [PointF]::new(170, 132)
    )

    Draw-Compass -Graphics $Graphics -X 202 -Y 52 -Radius 26 -RotationDegrees 18
    Draw-Climber -Graphics $Graphics -X 145 -Y 120 -Scale 0.72 -TiltDegrees 37 -PrimaryColor ([Color]::FromArgb(255, 77, 161, 229)) -AccentColor ([Color]::FromArgb(255, 240, 218, 182))
    Draw-Climber -Graphics $Graphics -X 168 -Y 154 -Scale 0.69 -TiltDegrees 37 -PrimaryColor ([Color]::FromArgb(255, 241, 110, 88)) -AccentColor ([Color]::FromArgb(255, 255, 223, 186))
    Draw-Climber -Graphics $Graphics -X 192 -Y 186 -Scale 0.64 -TiltDegrees 35 -PrimaryColor ([Color]::FromArgb(255, 145, 210, 96)) -AccentColor ([Color]::FromArgb(255, 231, 198, 155))
}

function Draw-OptionTwo {
    param(
        [Graphics]$Graphics,
        [int]$CanvasSize
    )

    Fill-BaseSky -Graphics $Graphics -CanvasSize $CanvasSize -TopColor ([Color]::FromArgb(255, 24, 36, 48)) -BottomColor ([Color]::FromArgb(255, 86, 101, 109))
    Add-AtmosphereStrokes -Graphics $Graphics -CanvasSize $CanvasSize -StrokeColor ([Color]::FromArgb(30, 230, 236, 238))

    $fogBrush = [SolidBrush]::new([Color]::FromArgb(26, 236, 248, 245))
    $Graphics.FillEllipse($fogBrush, -40, 110, 190, 150)
    $Graphics.FillEllipse($fogBrush, 20, 126, 150, 110)
    $fogBrush.Dispose()
    Draw-FogBank -Graphics $Graphics -CanvasSize $CanvasSize -YOffset 0
    Draw-FogRibbon -Graphics $Graphics -Points @(
        [PointF]::new(-10, 104),
        [PointF]::new(56, 82),
        [PointF]::new(116, 104),
        [PointF]::new(174, 90)
    ) -Color ([Color]::FromArgb(38, 245, 247, 246)) -Width 14

    $cliffPoints = @(
        [PointF]::new(255, 0),
        [PointF]::new(255, 255),
        [PointF]::new(116, 255),
        [PointF]::new(108, 208),
        [PointF]::new(140, 152),
        [PointF]::new(164, 109),
        [PointF]::new(188, 72),
        [PointF]::new(214, 42)
    )
    Draw-Mountain -Graphics $Graphics -Points $cliffPoints -FillColor ([Color]::FromArgb(255, 24, 29, 34)) -RimColor ([Color]::FromArgb(255, 63, 79, 92))
    Draw-RidgeHighlight -Graphics $Graphics -Points @(
        [PointF]::new(202, 58),
        [PointF]::new(182, 86),
        [PointF]::new(160, 120),
        [PointF]::new(140, 156),
        [PointF]::new(118, 212)
    )

    Draw-Compass -Graphics $Graphics -X 64 -Y 60 -Radius 28 -RotationDegrees -20

    Draw-Climber -Graphics $Graphics -X 171 -Y 114 -Scale 0.74 -TiltDegrees 32 -PrimaryColor ([Color]::FromArgb(255, 88, 166, 228)) -AccentColor ([Color]::FromArgb(255, 236, 213, 169))
    Draw-Climber -Graphics $Graphics -X 147 -Y 154 -Scale 0.70 -TiltDegrees 31 -PrimaryColor ([Color]::FromArgb(255, 250, 106, 86)) -AccentColor ([Color]::FromArgb(255, 255, 224, 181))
    Draw-Climber -Graphics $Graphics -X 126 -Y 190 -Scale 0.66 -TiltDegrees 28 -PrimaryColor ([Color]::FromArgb(255, 160, 218, 92)) -AccentColor ([Color]::FromArgb(255, 227, 195, 152))

    $pathGlow = @(
        [PointF]::new(205, 71),
        [PointF]::new(187, 99),
        [PointF]::new(167, 131),
        [PointF]::new(146, 166),
        [PointF]::new(130, 198)
    )
    Draw-GlowDots -Graphics $Graphics -Points $pathGlow -Radius 2.2 -Color ([Color]::FromArgb(160, 255, 219, 127))
}

function Draw-OptionThree {
    param(
        [Graphics]$Graphics,
        [int]$CanvasSize
    )

    Fill-BaseSky -Graphics $Graphics -CanvasSize $CanvasSize -TopColor ([Color]::FromArgb(255, 18, 31, 45)) -BottomColor ([Color]::FromArgb(255, 102, 118, 126))
    Add-AtmosphereStrokes -Graphics $Graphics -CanvasSize $CanvasSize -StrokeColor ([Color]::FromArgb(28, 214, 226, 234))
    Draw-FogBank -Graphics $Graphics -CanvasSize $CanvasSize -YOffset 12
    Draw-FogRibbon -Graphics $Graphics -Points @(
        [PointF]::new(22, 104),
        [PointF]::new(78, 84),
        [PointF]::new(148, 108),
        [PointF]::new(228, 92)
    ) -Color ([Color]::FromArgb(34, 239, 244, 248)) -Width 16

    $leftPeak = @(
        [PointF]::new(12, 223),
        [PointF]::new(86, 134),
        [PointF]::new(146, 220)
    )
    $rightPeak = @(
        [PointF]::new(92, 224),
        [PointF]::new(160, 76),
        [PointF]::new(246, 224)
    )
    Draw-Mountain -Graphics $Graphics -Points $leftPeak -FillColor ([Color]::FromArgb(255, 20, 27, 32)) -RimColor ([Color]::FromArgb(255, 58, 73, 82))
    Draw-Mountain -Graphics $Graphics -Points $rightPeak -FillColor ([Color]::FromArgb(255, 17, 24, 29)) -RimColor ([Color]::FromArgb(255, 70, 88, 100))
    Draw-RidgeHighlight -Graphics $Graphics -Points @(
        [PointF]::new(150, 96),
        [PointF]::new(160, 81),
        [PointF]::new(182, 116)
    )

    Draw-Compass -Graphics $Graphics -X 72 -Y 68 -Radius 34 -RotationDegrees 12
    Draw-Climber -Graphics $Graphics -X 118 -Y 172 -Scale 0.84 -TiltDegrees 35 -PrimaryColor ([Color]::FromArgb(255, 242, 121, 86)) -AccentColor ([Color]::FromArgb(255, 255, 226, 192))
    Draw-Climber -Graphics $Graphics -X 146 -Y 142 -Scale 0.68 -TiltDegrees 37 -PrimaryColor ([Color]::FromArgb(255, 82, 162, 224)) -AccentColor ([Color]::FromArgb(255, 238, 215, 176))
    Draw-Climber -Graphics $Graphics -X 171 -Y 112 -Scale 0.60 -TiltDegrees 40 -PrimaryColor ([Color]::FromArgb(255, 152, 214, 98)) -AccentColor ([Color]::FromArgb(255, 232, 198, 151))

    $flareBrush = [SolidBrush]::new([Color]::FromArgb(52, 255, 200, 125))
    $Graphics.FillEllipse($flareBrush, 122, 120, 80, 66)
    $flareBrush.Dispose()
}

function Draw-OptionFour {
    param(
        [Graphics]$Graphics,
        [int]$CanvasSize
    )

    Fill-BaseSky -Graphics $Graphics -CanvasSize $CanvasSize -TopColor ([Color]::FromArgb(255, 26, 36, 48)) -BottomColor ([Color]::FromArgb(255, 97, 111, 118))
    Add-AtmosphereStrokes -Graphics $Graphics -CanvasSize $CanvasSize -StrokeColor ([Color]::FromArgb(34, 226, 232, 236))
    Draw-FogBank -Graphics $Graphics -CanvasSize $CanvasSize -YOffset 6
    Draw-FogRibbon -Graphics $Graphics -Points @(
        [PointF]::new(-8, 54),
        [PointF]::new(62, 36),
        [PointF]::new(140, 70),
        [PointF]::new(242, 50)
    ) -Color ([Color]::FromArgb(42, 245, 246, 247)) -Width 18

    $cliffPoints = @(
        [PointF]::new(255, 0),
        [PointF]::new(255, 255),
        [PointF]::new(68, 255),
        [PointF]::new(86, 222),
        [PointF]::new(112, 182),
        [PointF]::new(138, 144),
        [PointF]::new(166, 104),
        [PointF]::new(194, 70),
        [PointF]::new(222, 36)
    )
    Draw-Mountain -Graphics $Graphics -Points $cliffPoints -FillColor ([Color]::FromArgb(255, 22, 28, 33)) -RimColor ([Color]::FromArgb(255, 68, 84, 96))
    Draw-RidgeHighlight -Graphics $Graphics -Points @(
        [PointF]::new(212, 48),
        [PointF]::new(188, 78),
        [PointF]::new(162, 116),
        [PointF]::new(136, 154),
        [PointF]::new(110, 194),
        [PointF]::new(90, 226)
    )

    Draw-Compass -Graphics $Graphics -X 204 -Y 50 -Radius 26 -RotationDegrees -8
    Draw-Climber -Graphics $Graphics -X 176 -Y 104 -Scale 0.80 -TiltDegrees 34 -PrimaryColor ([Color]::FromArgb(255, 83, 163, 229)) -AccentColor ([Color]::FromArgb(255, 239, 218, 184))
    Draw-Climber -Graphics $Graphics -X 149 -Y 144 -Scale 0.74 -TiltDegrees 33 -PrimaryColor ([Color]::FromArgb(255, 245, 111, 89)) -AccentColor ([Color]::FromArgb(255, 255, 225, 188))
    Draw-Climber -Graphics $Graphics -X 122 -Y 186 -Scale 0.68 -TiltDegrees 30 -PrimaryColor ([Color]::FromArgb(255, 150, 214, 95)) -AccentColor ([Color]::FromArgb(255, 228, 196, 151))
    Draw-GlowDots -Graphics $Graphics -Points @(
        [PointF]::new(188, 90),
        [PointF]::new(164, 126),
        [PointF]::new(138, 164),
        [PointF]::new(114, 204)
    ) -Radius 2.1 -Color ([Color]::FromArgb(144, 255, 224, 128))
}

function Draw-OptionFive {
    param(
        [Graphics]$Graphics,
        [int]$CanvasSize
    )

    Fill-BaseSky -Graphics $Graphics -CanvasSize $CanvasSize -TopColor ([Color]::FromArgb(255, 21, 33, 46)) -BottomColor ([Color]::FromArgb(255, 100, 114, 122))
    Add-AtmosphereStrokes -Graphics $Graphics -CanvasSize $CanvasSize -StrokeColor ([Color]::FromArgb(36, 225, 232, 236))
    Draw-FogBank -Graphics $Graphics -CanvasSize $CanvasSize -YOffset 20
    Draw-FogRibbon -Graphics $Graphics -Points @(
        [PointF]::new(-20, 128),
        [PointF]::new(44, 110),
        [PointF]::new(104, 134),
        [PointF]::new(178, 118),
        [PointF]::new(250, 142)
    ) -Color ([Color]::FromArgb(52, 238, 244, 246)) -Width 22

    $mountain = @(
        [PointF]::new(10, 220),
        [PointF]::new(84, 136),
        [PointF]::new(120, 98),
        [PointF]::new(150, 66),
        [PointF]::new(242, 220)
    )
    Draw-Mountain -Graphics $Graphics -Points $mountain -FillColor ([Color]::FromArgb(255, 18, 24, 29)) -RimColor ([Color]::FromArgb(255, 67, 84, 96))
    Draw-RidgeHighlight -Graphics $Graphics -Points @(
        [PointF]::new(100, 122),
        [PointF]::new(126, 92),
        [PointF]::new(148, 69),
        [PointF]::new(180, 114)
    )

    Draw-Compass -Graphics $Graphics -X 202 -Y 60 -Radius 24 -RotationDegrees 24
    Draw-Climber -Graphics $Graphics -X 131 -Y 140 -Scale 0.82 -TiltDegrees 36 -PrimaryColor ([Color]::FromArgb(255, 85, 165, 231)) -AccentColor ([Color]::FromArgb(255, 242, 220, 186))
    Draw-Climber -Graphics $Graphics -X 108 -Y 170 -Scale 0.72 -TiltDegrees 35 -PrimaryColor ([Color]::FromArgb(255, 245, 115, 92)) -AccentColor ([Color]::FromArgb(255, 255, 227, 191))
    Draw-Climber -Graphics $Graphics -X 88 -Y 198 -Scale 0.64 -TiltDegrees 32 -PrimaryColor ([Color]::FromArgb(255, 148, 212, 98)) -AccentColor ([Color]::FromArgb(255, 231, 198, 156))
}

$options = @(
    @{ Name = "icon-option-1.png"; Drawer = { param($g, $s) Draw-OptionOne -Graphics $g -CanvasSize $s } },
    @{ Name = "icon-option-2.png"; Drawer = { param($g, $s) Draw-OptionTwo -Graphics $g -CanvasSize $s } },
    @{ Name = "icon-option-3.png"; Drawer = { param($g, $s) Draw-OptionThree -Graphics $g -CanvasSize $s } },
    @{ Name = "icon-option-4.png"; Drawer = { param($g, $s) Draw-OptionFour -Graphics $g -CanvasSize $s } },
    @{ Name = "icon-option-5.png"; Drawer = { param($g, $s) Draw-OptionFive -Graphics $g -CanvasSize $s } }
)

foreach ($option in $options) {
    $canvas = New-Canvas -CanvasSize $size
    & $option.Drawer $canvas.Graphics $size
    Save-Canvas -Canvas $canvas -Path (Join-Path $projectRoot $option.Name)
}

Copy-Item (Join-Path $projectRoot "icon-option-4.png") -Destination (Join-Path $projectRoot "icon.png") -Force

Write-Host "Generated icon candidates:"
foreach ($option in $options) {
    Write-Host (Join-Path $projectRoot $option.Name)
}
Write-Host "Selected default icon:"
Write-Host (Join-Path $projectRoot "icon.png")
