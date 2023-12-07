using GitHubJwt;

using Octokit;

namespace IssuesOfDotNet.Crawler;

internal sealed class GitHubAppClient : GitHubClient
{
    private readonly int _appId;
    private readonly string _privateKey;

    private Installation? _installation;

    public GitHubAppClient(ProductHeaderValue productInformation, string appId, string privateKey)
        : base(productInformation)
    {
        if (appId is null)
            throw new ArgumentNullException(nameof(appId));

        if (privateKey is null)
            throw new ArgumentNullException(nameof(privateKey));

        _appId = Convert.ToInt32(appId);
        _privateKey = privateKey;
    }

    public void UseApplicationToken()
    {
        var token = GenerateAppToken();
        Credentials = new Credentials(token, AuthenticationType.Bearer);
    }

    public async Task UseInstallationTokenAsync(string account)
    {
        if (account is null)
            throw new ArgumentNullException(nameof(account));

        UseApplicationToken();

        var installations = await GitHubApps.GetAllInstallationsForCurrent();
        var installation = installations.SingleOrDefault(i => string.Equals(i.Account.Login, account));
        if (installation is null)
            throw new InvalidOperationException($"No installation found for '{account}'");

        await UseInstallationTokenAsync(installation);
    }

    public async Task UseInstallationTokenAsync(Installation installation)
    {
        if (installation is null)
            throw new ArgumentNullException(nameof(installation));

        UseApplicationToken();

        var installationTokenResult = await GitHubApps.CreateInstallationToken(installation.Id);
        Credentials = new Credentials(installationTokenResult.Token, AuthenticationType.Oauth);

        _installation = installation;
    }

    private string GenerateAppToken()
    {
        // See: https://octokitnet.readthedocs.io/en/latest/github-apps/ for details.

        var privateKeySource = new PlainStringPrivateKeySource(_privateKey);
        var generator = new GitHubJwtFactory(
            privateKeySource,
            new GitHubJwtFactoryOptions
            {
                AppIntegrationId = _appId,
                ExpirationSeconds = 8 * 60 // 600 is apparently too high
            });

        var token = generator.CreateEncodedJwtToken();
        return token;
    }

    private async Task RenewTokenAsync()
    {
        if (_installation is null)
            UseApplicationToken();
        else
            await UseInstallationTokenAsync(_installation);
    }

    public Task InvokeAsync(Func<GitHubClient, Task> operation)
    {
        return InvokeAsync<object?>(async c =>
        {
            await operation(c);
            return null;
        });
    }

    public async Task<T> InvokeAsync<T>(Func<GitHubClient, Task<T>> operation)
    {
        if (Credentials is null || Credentials.AuthenticationType == AuthenticationType.Anonymous)
        {
            Console.WriteLine($"Acquiring GitHub app token...");
            await RenewTokenAsync();
        }

        var remainingRetries = 3;

        while (true)
        {
            try
            {
                return await operation(this);
            }
            catch (RateLimitExceededException ex) when (remainingRetries > 0)
            {
                var delay = ex.GetRetryAfterTimeSpan()
                              .Add(TimeSpan.FromSeconds(15)); // Add some buffer
                var until = DateTime.Now.Add(delay);

                Console.WriteLine($"Rate limit exceeded. Waiting for {delay.TotalMinutes:N1} mins until {until}.");
                await Task.Delay(delay);
            }
            catch (AbuseException ex) when (remainingRetries > 0)
            {
                var delay = TimeSpan.FromSeconds(ex.RetryAfterSeconds ?? 120);
                var until = DateTime.Now.Add(delay);

                Console.WriteLine($"Abuse detection triggered. Waiting for {delay.TotalMinutes:N1} mins until {until}.");
                await Task.Delay(delay);
            }
            catch (AuthorizationException ex) when (remainingRetries > 0)
            {
                Console.WriteLine($"Authorization error: {ex.Message}. Refreshing token...");
                await RenewTokenAsync();
            }
            catch (OperationCanceledException) when (remainingRetries > 0)
            {
                Console.WriteLine($"Operation canceled. Assuming this means a token refresh is needed...");
                await RenewTokenAsync();
            }
        }
    }

    public sealed class PlainStringPrivateKeySource : IPrivateKeySource
    {
        private readonly string _key;

        public PlainStringPrivateKeySource(string key)
        {
            _key = key;
        }

        public TextReader GetPrivateKeyReader()
        {
            return new StringReader(_key);
        }
    }
}
