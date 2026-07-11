using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace MintADB.Wpf.Helpers;

/// <summary>
/// Smooth tab switches without leaving content at Opacity=0 (black screen).
/// Pattern: local Opacity base stays 1; animation From/To only overrides during play.
/// </summary>
public static class TabTransitionHelper
{
    private static int _generation;
    private const double DurationMs = 130;
    private const double SlidePx = 8;

    public static void Crossfade(UIElement? from, UIElement to, IEnumerable<UIElement>? alsoHide = null)
    {
        if (ReferenceEquals(from, to) && to.Visibility == Visibility.Visible)
            return;

        var gen = ++_generation;

        // Hide unrelated pages immediately — always reset Opacity to 1
        if (alsoHide is not null)
        {
            foreach (var el in alsoHide)
            {
                if (ReferenceEquals(el, to) || ReferenceEquals(el, from)) continue;
                HardHide(el);
            }
        }

        // Outgoing page
        if (from is not null && !ReferenceEquals(from, to) && from.Visibility == Visibility.Visible)
        {
            var fromEl = from;
            EnsureTransform(fromEl);

            var fadeOut = new DoubleAnimation
            {
                From = 1,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(DurationMs * 0.7),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn },
                FillBehavior = FillBehavior.Stop,
            };
            fadeOut.Completed += (_, _) =>
            {
                if (gen != _generation) return;
                HardHide(fromEl);
            };

            // Base stays 1 so if Stop fires early we don't stick at 0 forever
            fromEl.Opacity = 1;
            fromEl.BeginAnimation(UIElement.OpacityProperty, fadeOut);

            if (GetTranslate(fromEl) is { } tout)
            {
                tout.Y = 0;
                tout.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation
                {
                    From = 0,
                    To = -SlidePx * 0.4,
                    Duration = TimeSpan.FromMilliseconds(DurationMs * 0.7),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn },
                    FillBehavior = FillBehavior.Stop,
                });
            }
        }
        else if (from is not null && !ReferenceEquals(from, to))
        {
            HardHide(from);
        }

        // Incoming page — CRITICAL: local Opacity must be 1 before animating From=0
        EnsureTransform(to);
        ClearAnimations(to);
        to.Visibility = Visibility.Visible;
        to.Opacity = 1; // base value after FillBehavior.Stop
        if (GetTranslate(to) is { } t0)
            t0.Y = 0;

        var fadeIn = new DoubleAnimation
        {
            From = 0,
            To = 1,
            Duration = TimeSpan.FromMilliseconds(DurationMs),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            FillBehavior = FillBehavior.Stop, // reverts to local value = 1 ✓
        };
        fadeIn.Completed += (_, _) =>
        {
            if (gen != _generation) return;
            ClearAnimations(to);
            to.Opacity = 1;
            if (GetTranslate(to) is { } t) t.Y = 0;
        };

        to.BeginAnimation(UIElement.OpacityProperty, fadeIn);

        if (GetTranslate(to) is { } tin)
        {
            tin.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation
            {
                From = SlidePx,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(DurationMs),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
                FillBehavior = FillBehavior.Stop,
            });
        }
    }

    public static void ShowOnly(UIElement show, params UIElement[] hide)
    {
        ++_generation;
        foreach (var el in hide)
            HardHide(el);
        HardShow(show);
    }

    private static void HardHide(UIElement el)
    {
        ClearAnimations(el);
        el.Opacity = 1;
        el.Visibility = Visibility.Collapsed;
        if (GetTranslate(el) is { } t) t.Y = 0;
    }

    private static void HardShow(UIElement el)
    {
        ClearAnimations(el);
        el.Opacity = 1;
        el.Visibility = Visibility.Visible;
        if (GetTranslate(el) is { } t) t.Y = 0;
    }

    private static void ClearAnimations(UIElement el)
    {
        el.BeginAnimation(UIElement.OpacityProperty, null);
        if (GetTranslate(el) is { } t)
            t.BeginAnimation(TranslateTransform.YProperty, null);
    }

    private static void EnsureTransform(UIElement el)
    {
        if (el is not FrameworkElement fe) return;
        if (fe.RenderTransform is not TranslateTransform)
        {
            fe.RenderTransform = new TranslateTransform();
            fe.RenderTransformOrigin = new Point(0.5, 0);
        }
    }

    private static TranslateTransform? GetTranslate(UIElement el)
        => el is FrameworkElement { RenderTransform: TranslateTransform t } ? t : null;
}
