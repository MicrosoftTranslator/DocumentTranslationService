# Microsoft Document Translation Service Submodule

This is a library written for .Net 5, wrapping the Microsoft Translator document and text translation services.
It is used by the [Document Translation](https://github.com/MicrosoftTranslator/DocumentTranslation) solution
as well as the [Translator Web](https://github.com/MicrosoftTranslator/TranslatorBlazor) Blazor implementation,
appearing in both as a Git submodule.

## Methods

It exposes the following objects:
- DocumentTranslationService
  -  Drives all functionality of Microsoft's Document Translation service with simple
methods. Typically only async methods are implemented, because document translation offers long-running processes. 
Feedback from the long-running processes is typically returned via events. 
  - Language enumeration
  - Text translation, with plain text and HTML snippet translation, sentence breaking, and chunking functions.
- DocumentTranslationBusiness encapsulates all local file functions, for reading, filtering and for
copying local files from and to Azure Blob storage.


## Usage
If you want to use it in your own solution, add to your project like this:

`git submodule add https://github.com/MicrosoftTranslator/DocumentTranslationService`

This will add a folder 'DocumentTranslationService' to your solution.
Add DocumentTranslationService.csproj into your solution as another project.

See the [Document Translation](https://github.com/MicrosoftTranslator/DocumentTranslation) and
[Translator Web](https://github.com/MicrosoftTranslator/TranslatorBlazor) repos for examples.

After you have added this as a submodule to your solution, it will not automatically update when you
fetch updates for your repo. To update to the newest version of this submodule, issue a

`git submodule update --remote --merge`

command. It will refresh to the newest code version.

### Warning
An update may include a breaking change and may require you to modify your code using this submodule.


## Credits
The tool uses following Nuget packages:
- Azure.Storage.Blobs for the interaction with the Azure storage service. 
- Azure.AI.Translation.Document, a client library for the Azure Document Translation Service
- Azure.Identity for authentication to Key Vault
- Azure.Security.KeyVault.Secrets for reading the credentials from Azure Key Vault

Our sincere thanks to the authors of these packages.
