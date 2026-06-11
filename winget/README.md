# winget packaging

Manifests for publishing Sunglasses to the
[winget community repo](https://github.com/microsoft/winget-pkgs).

Package identifier: **`mkronvold.Sunglasses`** (moniker: `sunglasses`).

## Release & publish steps

1. **Tag a release** so the GitHub Actions workflow builds and publishes the
   self-contained `Sunglasses.exe`:

   ```powershell
   git tag v1.0.0
   git push origin v1.0.0
   ```

   The workflow attaches `Sunglasses.exe` to the release and prints the
   **InstallerSha256** (in the run summary and release notes).

2. **Update the manifest** for the new version:
   - Copy the `1.0.0/` folder to the new version number.
   - Bump `PackageVersion` in all three files.
   - Update `InstallerUrl` to the new tag and paste the real `InstallerSha256`.

3. **Validate locally** (requires winget):

   ```powershell
   winget validate --manifest winget/manifests/m/mkronvold/Sunglasses/1.0.0
   winget install --manifest winget/manifests/m/mkronvold/Sunglasses/1.0.0
   ```

4. **Submit** to winget-pkgs. Easiest with
   [wingetcreate](https://github.com/microsoft/winget-create):

   ```powershell
   winget install Microsoft.WingetCreate
   wingetcreate update mkronvold.Sunglasses `
     --version 1.0.0 `
     --urls https://github.com/mkronvold/sunglasses/releases/download/v1.0.0/Sunglasses.exe `
     --submit
   ```

   Once the PR is merged, `winget install mkronvold.Sunglasses` (or
   `winget install sunglasses`) works for everyone.

> Note: the `InstallerSha256` in `1.0.0/` is a placeholder until the first
> release is built; fill it from the workflow output before submitting.
