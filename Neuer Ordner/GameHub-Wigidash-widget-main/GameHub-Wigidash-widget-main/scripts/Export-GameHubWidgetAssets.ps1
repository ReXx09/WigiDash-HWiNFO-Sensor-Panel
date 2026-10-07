param(
    [string]$OutputRoot
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    throw "OutputRoot is required."
}

Add-Type -AssemblyName System.Drawing

function New-RoundedRectPath {
    param(
        [System.Drawing.Rectangle]$Bounds,
        [int]$Radius
    )

    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $diameter = $Radius * 2
    $path.AddArc($Bounds.X, $Bounds.Y, $diameter, $diameter, 180, 90)
    $path.AddArc($Bounds.Right - $diameter, $Bounds.Y, $diameter, $diameter, 270, 90)
    $path.AddArc($Bounds.Right - $diameter, $Bounds.Bottom - $diameter, $diameter, $diameter, 0, 90)
    $path.AddArc($Bounds.X, $Bounds.Bottom - $diameter, $diameter, $diameter, 90, 90)
    $path.CloseFigure()
    return $path
}

function Write-Thumbnail {
    param([string]$Path)

    $bitmap = New-Object System.Drawing.Bitmap 145, 145
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)

    $backgroundColor = [System.Drawing.Color]::FromArgb(28, 28, 30)
    $panelColor = [System.Drawing.Color]::FromArgb(38, 38, 42)
    $borderColor = [System.Drawing.Color]::FromArgb(72, 72, 78)
    $accentColor = [System.Drawing.Color]::FromArgb(255, 142, 52)
    $mutedColor = [System.Drawing.Color]::FromArgb(188, 188, 194)

    $background = New-Object System.Drawing.SolidBrush $backgroundColor
    $panel = New-Object System.Drawing.SolidBrush $panelColor
    $accent = New-Object System.Drawing.SolidBrush $accentColor
    $white = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::White)
    $muted = New-Object System.Drawing.SolidBrush $mutedColor
    $border = New-Object System.Drawing.Pen $borderColor
    $monogramFont = New-Object System.Drawing.Font "Segoe UI Semibold", 31
    $titleFont = New-Object System.Drawing.Font "Segoe UI Semibold", 10
    $subtitleFont = New-Object System.Drawing.Font "Segoe UI", 8
    $centered = New-Object System.Drawing.StringFormat
    $centered.Alignment = [System.Drawing.StringAlignment]::Center
    $centered.LineAlignment = [System.Drawing.StringAlignment]::Center

    try {
        $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $graphics.Clear($backgroundColor)
        $graphics.FillRectangle($background, 0, 0, 145, 145)

        $outer = New-Object System.Drawing.Rectangle 8, 8, 129, 129
        $graphics.FillRectangle($panel, $outer)
        $graphics.DrawRectangle($border, $outer)
        $graphics.FillRectangle($accent, $outer.X, $outer.Y, $outer.Width, 8)

        $badge = New-Object System.Drawing.Rectangle ($outer.X + 18), ($outer.Y + 24), ($outer.Width - 36), 58
        $badgePath = New-RoundedRectPath -Bounds $badge -Radius 14
        $badgeBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(48, 52, 60))
        $graphics.FillPath($badgeBrush, $badgePath)
        $graphics.DrawString("GH", $monogramFont, $white, (New-Object System.Drawing.RectangleF $badge.X, $badge.Y, $badge.Width, $badge.Height), $centered)

        $graphics.FillEllipse($accent, $badge.Right - 18, $badge.Y + 10, 8, 8)
        $graphics.FillEllipse($accent, $badge.Right - 32, $badge.Y + 26, 8, 8)
        $graphics.FillEllipse($accent, $badge.Right - 4, $badge.Y + 26, 8, 8)

        $graphics.DrawString("GAME HUB", $titleFont, $white, (New-Object System.Drawing.RectangleF ($outer.X), ($outer.Y + 93), $outer.Width, 16), $centered)
        $graphics.DrawString("Launcher widget", $subtitleFont, $muted, (New-Object System.Drawing.RectangleF ($outer.X), ($outer.Y + 108), $outer.Width, 14), $centered)

        $bitmap.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $centered.Dispose()
        $subtitleFont.Dispose()
        $titleFont.Dispose()
        $monogramFont.Dispose()
        $border.Dispose()
        $muted.Dispose()
        $white.Dispose()
        $accent.Dispose()
        $panel.Dispose()
        $background.Dispose()
        $graphics.Dispose()
        $bitmap.Dispose()
        if ($badgeBrush) { $badgeBrush.Dispose() }
        if ($badgePath) { $badgePath.Dispose() }
    }
}

function Write-Preview {
    param([string]$Path)

    $bitmap = New-Object System.Drawing.Bitmap 1010, 580
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)

    $accentColor = [System.Drawing.Color]::FromArgb(255, 142, 52)
    $accentSoftColor = [System.Drawing.Color]::FromArgb(163, 74, 36)
    $panelColor = [System.Drawing.Color]::FromArgb(48, 32, 28)
    $selectedPanelColor = [System.Drawing.Color]::FromArgb(82, 50, 40)
    $backgroundColor = [System.Drawing.Color]::FromArgb(16, 10, 9)
    $cardColor = [System.Drawing.Color]::FromArgb(33, 20, 18)
    $sidebarColor = [System.Drawing.Color]::FromArgb(24, 15, 14)
    $borderColor = [System.Drawing.Color]::FromArgb(142, 94, 72)
    $mutedColor = [System.Drawing.Color]::FromArgb(218, 201, 192)

    $background = New-Object System.Drawing.SolidBrush $backgroundColor
    $card = New-Object System.Drawing.SolidBrush $cardColor
    $sidebar = New-Object System.Drawing.SolidBrush $sidebarColor
    $panel = New-Object System.Drawing.SolidBrush $panelColor
    $selectedPanel = New-Object System.Drawing.SolidBrush $selectedPanelColor
    $accent = New-Object System.Drawing.SolidBrush $accentColor
    $accentSoft = New-Object System.Drawing.SolidBrush $accentSoftColor
    $white = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::White)
    $muted = New-Object System.Drawing.SolidBrush $mutedColor
    $border = New-Object System.Drawing.Pen $borderColor
    $titleFont = New-Object System.Drawing.Font "Segoe UI Semibold", 15
    $bodyFont = New-Object System.Drawing.Font "Segoe UI Semibold", 9.5
    $smallFont = New-Object System.Drawing.Font "Segoe UI", 8.5
    $centered = New-Object System.Drawing.StringFormat
    $centered.Alignment = [System.Drawing.StringAlignment]::Center
    $centered.LineAlignment = [System.Drawing.StringAlignment]::Center

    try {
        $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $graphics.Clear($backgroundColor)
        $graphics.FillRectangle($background, 0, 0, 1010, 580)

        $outer = New-Object System.Drawing.Rectangle 10, 10, 990, 560
        $graphics.FillRectangle($card, $outer)
        $graphics.DrawRectangle($border, $outer)

        $sidebarRect = New-Object System.Drawing.Rectangle ($outer.X + 10), ($outer.Y + 10), 190, ($outer.Height - 20)
        $mainRect = New-Object System.Drawing.Rectangle ($sidebarRect.Right + 10), ($outer.Y + 10), ($outer.Right - $sidebarRect.Right - 20), ($outer.Height - 20)
        $graphics.FillRectangle($sidebar, $sidebarRect)
        $graphics.DrawRectangle($border, $sidebarRect)

        $clockRect = New-Object System.Drawing.Rectangle ($sidebarRect.X + 8), ($sidebarRect.Y + 8), ($sidebarRect.Width - 16), 48
        $actionOne = New-Object System.Drawing.Rectangle ($sidebarRect.X + 8), ($clockRect.Bottom + 8), ($sidebarRect.Width - 16), 42
        $actionTwo = New-Object System.Drawing.Rectangle ($sidebarRect.X + 8), ($actionOne.Bottom + 8), ($sidebarRect.Width - 16), 42
        $themeRect = New-Object System.Drawing.Rectangle ($sidebarRect.X + 8), ($sidebarRect.Bottom - 58), ($sidebarRect.Width - 16), 24
        $configRect = New-Object System.Drawing.Rectangle ($sidebarRect.X + 8), ($sidebarRect.Bottom - 30), ($sidebarRect.Width - 16), 22

        foreach ($rect in @($clockRect, $actionOne, $actionTwo, $themeRect, $configRect)) {
            $graphics.FillRectangle($panel, $rect)
            $graphics.DrawRectangle($border, $rect)
        }

        $graphics.DrawString("16:42", $titleFont, $white, (New-Object System.Drawing.PointF ($clockRect.X + 8), ($clockRect.Y + 10)))
        $graphics.DrawString("CPU 61C", $smallFont, $muted, (New-Object System.Drawing.PointF ($clockRect.X + 8), ($clockRect.Y + 30)))
        $graphics.DrawString("Steam", $bodyFont, $white, (New-Object System.Drawing.RectangleF ($actionOne.X), ($actionOne.Y + 12), $actionOne.Width, 18), $centered)
        $graphics.DrawString("Temps", $bodyFont, $white, (New-Object System.Drawing.RectangleF ($actionTwo.X), ($actionTwo.Y + 12), $actionTwo.Width, 18), $centered)
        $graphics.DrawString("Theme", $smallFont, $muted, (New-Object System.Drawing.RectangleF ($themeRect.X), ($themeRect.Y + 3), $themeRect.Width, 10), $centered)
        $graphics.DrawString("Steel", $smallFont, $white, (New-Object System.Drawing.RectangleF ($themeRect.X), ($themeRect.Y + 11), $themeRect.Width, 10), $centered)
        $graphics.DrawString("Config", $smallFont, $white, (New-Object System.Drawing.RectangleF ($configRect.X), ($configRect.Y + 4), $configRect.Width, 12), $centered)

        $graphics.DrawString("Game Hub", $titleFont, $white, (New-Object System.Drawing.PointF ($mainRect.X), ($mainRect.Y)))
        $graphics.DrawString("v1.0.0", $smallFont, $muted, (New-Object System.Drawing.PointF ($mainRect.Right - 42), ($mainRect.Y + 4)))

        $filtersRect = New-Object System.Drawing.Rectangle ($mainRect.X), ($mainRect.Y + 30), $mainRect.Width, 32
        $filterWidth = [int](($filtersRect.Width - 24) / 4)
        $labels = @("All", "Steam", "EA", "GOG")
        for ($i = 0; $i -lt 4; $i++) {
            $filter = New-Object System.Drawing.Rectangle ($filtersRect.X + $i * ($filterWidth + 8)), $filtersRect.Y, $filterWidth, $filtersRect.Height
            $graphics.FillRectangle($(if ($i -eq 0) { $accent } else { $panel }), $filter)
            $graphics.DrawRectangle($(if ($i -eq 0) { New-Object System.Drawing.Pen $accentColor, 2 } else { $border }), $filter)
            $graphics.DrawString($labels[$i], $bodyFont, $white, (New-Object System.Drawing.RectangleF ($filter.X), ($filter.Y + 8), $filter.Width, 16), $centered)
        }

        $gridRect = New-Object System.Drawing.Rectangle ($mainRect.X), ($filtersRect.Bottom + 8), $mainRect.Width, ($mainRect.Height - 110)
        $columns = 3
        $rows = 2
        $gap = 8
        $tileWidth = [int](($gridRect.Width - ($gap * ($columns - 1))) / $columns)
        $tileHeight = [int](($gridRect.Height - ($gap * ($rows - 1))) / $rows)
        $names = @("Overwatch", "Sea of Thieves", "Dungeons 2", "StarCraft", "Assassin's Creed", "Cyberpunk 2077")
        $artColors = @(
            [System.Drawing.Color]::FromArgb(95, 58, 46),
            [System.Drawing.Color]::FromArgb(59, 71, 93),
            [System.Drawing.Color]::FromArgb(71, 48, 83),
            [System.Drawing.Color]::FromArgb(44, 76, 65),
            [System.Drawing.Color]::FromArgb(85, 61, 42),
            [System.Drawing.Color]::FromArgb(67, 46, 46)
        )

        for ($row = 0; $row -lt $rows; $row++) {
            for ($column = 0; $column -lt $columns; $column++) {
                $index = $row * $columns + $column
                $tile = New-Object System.Drawing.Rectangle ($gridRect.X + $column * ($tileWidth + $gap)), ($gridRect.Y + $row * ($tileHeight + $gap)), $tileWidth, $tileHeight
                $graphics.FillRectangle($(if ($index -eq 0) { $selectedPanel } else { $panel }), $tile)
                $graphics.DrawRectangle($border, $tile)

                $artRect = New-Object System.Drawing.Rectangle ($tile.X + 10), ($tile.Y + 10), ($tile.Width - 20), ($tile.Height - 34)
                $artBrush = New-Object System.Drawing.SolidBrush $artColors[$index]
                $graphics.FillRectangle($artBrush, $artRect)
                $graphics.FillRectangle($accentSoft, (New-Object System.Drawing.Rectangle ($artRect.X + 12), ($artRect.Y + 12), ($artRect.Width - 24), ($artRect.Height - 24)))
                $graphics.DrawString($names[$index], $bodyFont, $white, (New-Object System.Drawing.RectangleF ($tile.X + 8), ($tile.Bottom - 22), ($tile.Width - 16), 16))
                $artBrush.Dispose()
            }
        }

        $footerRect = New-Object System.Drawing.Rectangle ($mainRect.X), ($mainRect.Bottom - 34), $mainRect.Width, 28
        $graphics.FillRectangle($panel, $footerRect)
        $graphics.DrawRectangle($border, $footerRect)
        $graphics.FillRectangle($accent, (New-Object System.Drawing.Rectangle ($footerRect.Right - 92), ($footerRect.Y + 4), 84, ($footerRect.Height - 8)))
        $graphics.DrawString("Overwatch selected", $bodyFont, $muted, (New-Object System.Drawing.PointF ($footerRect.X + 10), ($footerRect.Y + 6)))
        $graphics.DrawString("Launch", $bodyFont, $white, (New-Object System.Drawing.RectangleF ($footerRect.Right - 92), ($footerRect.Y + 6), 84, 14), $centered)

        $bitmap.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $centered.Dispose()
        $smallFont.Dispose()
        $bodyFont.Dispose()
        $titleFont.Dispose()
        $border.Dispose()
        $muted.Dispose()
        $white.Dispose()
        $accentSoft.Dispose()
        $accent.Dispose()
        $selectedPanel.Dispose()
        $panel.Dispose()
        $sidebar.Dispose()
        $card.Dispose()
        $background.Dispose()
        $graphics.Dispose()
        $bitmap.Dispose()
    }
}

New-Item -ItemType Directory -Path $OutputRoot -Force | Out-Null
Write-Thumbnail -Path (Join-Path $OutputRoot "thumb.png")
Write-Preview -Path (Join-Path $OutputRoot "preview_5x4.png")

Write-Host "Exported widget assets to $OutputRoot"
