namespace ArrDash.Services;

public sealed class LayoutSettingsSaveService(
    LayoutPreferencesService prefs,
    ServiceCredentialsPreviewService credentialsPreview,
    ServiceSecretsStore secrets,
    MediaServiceOptionsAccessor optionsAccessor,
    IDashboardRefresher refresh)
{
    public async Task SavePreviewAsync(CancellationToken ct = default)
    {
        var current = prefs.Current;
        await prefs.SaveAsync(current, ct);

        var pendingSecrets = credentialsPreview.TakePendingUpdates();
        if (pendingSecrets is not null)
        {
            await secrets.SavePartialAsync(pendingSecrets, ct);
            optionsAccessor.Reload();
        }

        await refresh.RefreshAsync(ct);
    }
}
