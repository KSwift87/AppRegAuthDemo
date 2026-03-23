NuGet Packages:
# Client
	- Microsoft.AspNetCore.Authentication
	- Microsoft.AspNetCore.Authentication.Cookies
	- Microsoft.AspNetCore.Authentication.OpenIdConnect
	- Microsoft.Identity.Web.UI
# Server
	- Microsoft.Identity.Web
	- Microsoft.IdentityModel.Protocols
	- Microsoft.IdentityModel.Protocols.OpenIdConnect
	- Microsoft.IdentityModel.Tokens

Log into Azure portal, and pull up five new tabs:
	1. Microsoft Entra ID
	2. App Registrations (for the client app)
	3. App Registrations (for the server app)
	4. Go to jwt.io (we're going to place the access token here).
	5. Go to jwt.io again (we're going to place the id token here).

When creating your app registrations, be sure to assign yourself as the owner or otherwise 
you will not be able to Request API Permissions!