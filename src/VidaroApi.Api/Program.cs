var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.UseHttpsRedirection();

// Feature endpoints registered here as slices are implemented:
// RegisterUser.MapEndpoint(app);

app.Run();
