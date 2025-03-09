using CarRentalAPI.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);


// Add CORS services and define a policy
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        // Allow the UI origin (adjust port if needed)
        policy.WithOrigins("http://localhost:5163")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});


// Definer en API-nøkkel for enkel autentisering
const string API_KEY = "mysecret123";

// Legg til DbContext med SQLite
builder.Services.AddDbContext<CarRentalDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// Legg til støtte for Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "CarRentalAPI", Version = "v1" });


    // Legg til en “ApiKey” definisjon
    c.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
    {
        Description = "Bruk denne for å autorisere forespørsler. Legg inn X-API-Key her.",
        In = ParameterLocation.Header, // API-nøkkelen sendes i header
        Name = "X-API-Key",
        Type = SecuritySchemeType.ApiKey,
        Scheme = "ApiKeyScheme"
    });

    // Krev at alle sikre endepunkter skal bruke denne definisjonen
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "ApiKey"
                }
            },
            new List<string>()
        }
    });
});

var app = builder.Build();

// Enable CORS middleware before endpoints
app.UseCors();


if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// -----------------
// Offentlige endepunkter
// -----------------

// Velkomst-endepunkt
app.MapGet("/", () => "Velkommen til Bilutleie API!");

// Endepunkt for å legge til en kunde (POST /customers)
app.MapPost("/customers", async (CarRentalDbContext db, Customer customer) =>
{
    db.Customers.Add(customer);
    await db.SaveChangesAsync();
    return Results.Created($"/customers/{customer.Id}", customer);
});

// Endepunkt for å hente alle kunder (GET /customers)
app.MapGet("/customers", async (CarRentalDbContext db) =>
    await db.Customers.ToListAsync());

// Endepunkt for å legge til en bil (POST /cars)
app.MapPost("/cars", async (CarRentalDbContext db, Car car) =>
{
    db.Cars.Add(car);
    await db.SaveChangesAsync();
    return Results.Created($"/cars/{car.Id}", car);
});

// Endepunkt for å hente alle biler (GET /cars)
app.MapGet("/cars", async (CarRentalDbContext db) =>
    await db.Cars.ToListAsync());

// Endepunkt for å opprette et leieforhold (POST /rentals)
app.MapPost("/rentals", async (CarRentalDbContext db, Rental rental) =>
{
    // Valider at hvis RentalEnd er angitt, må RentalStart være før RentalEnd
    if (rental.RentalEnd != null && rental.RentalStart >= rental.RentalEnd)
    {
        return Results.BadRequest("RentalStart must be earlier than RentalEnd.");
    }

    // Sjekk for overlapping for samme bil og samme kunde
    bool overlap = await db.Rentals.AnyAsync(r =>
        r.CarId == rental.CarId &&
        r.CustomerId == rental.CustomerId &&
        (r.RentalEnd ?? DateTime.MaxValue) >= rental.RentalStart &&
        (rental.RentalEnd ?? DateTime.MaxValue) >= r.RentalStart
    );

    if (overlap)
    {
        return Results.Conflict("Samme kunde har allerede denne bilen i et overlappende tidsrom.");
    }

    // Sjekk om bilen er utleid til en annen kunde i samme periode
    bool carInUse = await db.Rentals.AnyAsync(r =>
        r.CarId == rental.CarId &&
        r.CustomerId != rental.CustomerId &&
        (r.RentalEnd ?? DateTime.MaxValue) >= rental.RentalStart &&
        (rental.RentalEnd ?? DateTime.MaxValue) >= r.RentalStart
    );

    if (carInUse)
    {
        return Results.Conflict("Bilen er allerede utleid til en annen kunde i det angitte tidsrommet.");
    }

    db.Rentals.Add(rental);
    await db.SaveChangesAsync();
    return Results.Created($"/rentals/{rental.Id}", rental);
});

// Endepunkt for å hente alle leieforhold (GET /rentals/all)
app.MapGet("/rentals/all", async (CarRentalDbContext db) =>
    await db.Rentals.ToListAsync());

// Endepunkt for å hente aktive leieforhold (GET /rentals/active)
// Her defineres "aktivt" som at nåværende tid er innenfor [RentalStart, RentalEnd]
app.MapGet("/rentals/active", async (CarRentalDbContext db) =>
{
    var now = DateTime.UtcNow;
    return await db.Rentals
        .Where(r => r.RentalStart <= now && r.RentalEnd >= now)
        .ToListAsync();
});

// -----------------
// SIKRE (Autentiserte) endepunkter for oppdatering og sletting
// -----------------

// Oppdater kunde (PUT /customers/{id})
app.MapPut("/customers/{id}", async (int id, Customer updatedCustomer, CarRentalDbContext db, HttpRequest request) =>
{
    if (!request.Headers.TryGetValue("X-API-Key", out var extractedApiKey) || extractedApiKey != API_KEY)
        return Results.Unauthorized();

    var customer = await db.Customers.FindAsync(id);
    if (customer == null)
        return Results.NotFound();

    customer.Name = updatedCustomer.Name;
    await db.SaveChangesAsync();
    return Results.Ok(customer);
});

// Slett kunde (DELETE /customers/{id})
app.MapDelete("/customers/{id}", async (int id, CarRentalDbContext db, HttpRequest request) =>
{
    if (!request.Headers.TryGetValue("X-API-Key", out var extractedApiKey) || extractedApiKey != API_KEY)
        return Results.Unauthorized();

    var customer = await db.Customers.FindAsync(id);
    if (customer == null)
        return Results.NotFound();

    db.Customers.Remove(customer);
    await db.SaveChangesAsync();
    return Results.NoContent();
});

// Oppdater bil (PUT /cars/{id})
app.MapPut("/cars/{id}", async (int id, Car updatedCar, CarRentalDbContext db, HttpRequest request) =>
{
    if (!request.Headers.TryGetValue("X-API-Key", out var extractedApiKey) || extractedApiKey != API_KEY)
        return Results.Unauthorized();

    var car = await db.Cars.FindAsync(id);
    if (car == null)
        return Results.NotFound();

    car.Model = updatedCar.Model;
    await db.SaveChangesAsync();
    return Results.Ok(car);
});

// Slett bil (DELETE /cars/{id})
app.MapDelete("/cars/{id}", async (int id, CarRentalDbContext db, HttpRequest request) =>
{
    if (!request.Headers.TryGetValue("X-API-Key", out var extractedApiKey) || extractedApiKey != API_KEY)
        return Results.Unauthorized();

    var car = await db.Cars.FindAsync(id);
    if (car == null)
        return Results.NotFound();

    db.Cars.Remove(car);
    await db.SaveChangesAsync();
    return Results.NoContent();
});

// Oppdater leieforhold (PUT /rentals/{id})
app.MapPut("/rentals/{id}", async (int id, Rental updatedRental, CarRentalDbContext db, HttpRequest request) =>
{
    if (!request.Headers.TryGetValue("X-API-Key", out var extractedApiKey) || extractedApiKey != API_KEY)
        return Results.Unauthorized();

    var rental = await db.Rentals.FindAsync(id);
    if (rental == null)
        return Results.NotFound();

    // Valider datoer: hvis RentalEnd er angitt, må RentalStart være før RentalEnd
    if (updatedRental.RentalEnd != null && updatedRental.RentalStart >= updatedRental.RentalEnd)
        return Results.BadRequest("RentalStart must be earlier than RentalEnd.");

    rental.RentalStart = updatedRental.RentalStart;
    rental.RentalEnd = updatedRental.RentalEnd;
    rental.CustomerId = updatedRental.CustomerId;
    rental.CarId = updatedRental.CarId;

    await db.SaveChangesAsync();
    return Results.Ok(rental);
});

// Slett leieforhold (DELETE /rentals/{id})
app.MapDelete("/rentals/{id}", async (int id, CarRentalDbContext db, HttpRequest request) =>
{
    if (!request.Headers.TryGetValue("X-API-Key", out var extractedApiKey) || extractedApiKey != API_KEY)
        return Results.Unauthorized();

    var rental = await db.Rentals.FindAsync(id);
    if (rental == null)
        return Results.NotFound();

    db.Rentals.Remove(rental);
    await db.SaveChangesAsync();
    return Results.NoContent();
});

app.Run();
