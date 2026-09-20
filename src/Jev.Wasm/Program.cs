using Jev.App;
using Jev.Wasm;
using Jev.Workers;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddSingleton<ICodingHost>(CodingHost.Restricted("wasm"));
builder.Services.AddJevApp(builder.Configuration);

await builder.Build().RunAsync();
