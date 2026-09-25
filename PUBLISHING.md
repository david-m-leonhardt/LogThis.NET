# Publishing LogThis.NET

The manual GitHub Actions workflow at `.github/workflows/publish-nuget.yml` packs
and publishes both packages. Do not run it until the release version and README
are ready; NuGet.org does not allow replacing an existing package version.

## One-time setup

1. On NuGet.org, create a Trusted Publishing policy owned by your account.
   Select GitHub Actions, repository owner `david-m-leonhardt`, repository
   `LogThis.NET`, and workflow filename `publish-nuget.yml`. Leave the optional
   environment blank. Allow both new package creation and new versions, and
   scope the package IDs to `LogThis.NET*` if the form offers a glob pattern.
2. In GitHub repository Settings > Secrets and variables > Actions > Variables,
   create `NUGET_USER` with your NuGet.org **username**, not your email address.
   No long-lived API key or signing certificate is needed for this workflow.

## Each release

1. Set the shared version in `Directory.Build.props`, and update any versioned
   install commands in `README.md`. Commit and push the release source. Merge
   the workflow into the repository's default `main` branch so its manual
   trigger appears in GitHub Actions.
2. In GitHub Actions, choose **Publish NuGet packages**, select the release
   branch, and click **Run workflow**. The workflow builds both packages,
   obtains a short-lived publishing key, then pushes `LogThis.NET` before
   `LogThis.NET.AspNetCore`.
3. Wait for NuGet.org validation and indexing. Confirm both package pages and
   install the published packages in fresh console and ASP.NET Core projects.

The workflow uses `--skip-duplicate` so a failed run can be retried from the
**same commit** if one package was already accepted. If source changes after
publishing either package, increase the shared version before running again.
