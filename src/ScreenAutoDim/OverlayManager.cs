namespace ScreenAutoDim;

/// <summary>Keeps exactly one <see cref="OverlayForm"/> per connected monitor.</summary>
internal sealed class OverlayManager : IDisposable
{
    private readonly Dictionary<Rectangle, OverlayForm> _forms = new();

    /// <param name="dimming">0 = no dimming, 1 = fully black.</param>
    public void Apply(double dimming)
    {
        var current = Screen.AllScreens.Select(s => s.Bounds).ToHashSet();

        foreach (var gone in _forms.Keys.Where(b => !current.Contains(b)).ToList())
        {
            _forms[gone].Close();
            _forms.Remove(gone);
        }

        foreach (var bounds in current.Where(b => !_forms.ContainsKey(b)))
        {
            var form = new OverlayForm(bounds);
            _forms[bounds] = form;
            form.Show();
        }

        foreach (var form in _forms.Values)
        {
            form.SetDimming(dimming);
            form.Reposition();
        }
    }

    public void Dispose()
    {
        foreach (var form in _forms.Values)
            form.Close();
        _forms.Clear();
    }
}
