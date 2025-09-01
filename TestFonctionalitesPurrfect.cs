using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PurrfectMates.Api.Data;
using PurrfectMates.Enums;
using PurrfectMates.Models;

namespace PurrfectMates.Tests
{
    [TestFixture]
    public class AnimalsControllerSqlServerTest
    {
        private IConfiguration _config = default!;
        private string _dbName = default!;
        private string _connexionMaster = default!;
        private string _connexionTest = default!;
        private string _connexionTpl = default!;


        private AppDbContext _db = default!;
        private PurrfectMates.Api.Controllers.AnimalsController _controller = default!;


        [OneTimeSetUp]
        public void OneTimeSetup()
        {
            _config = new ConfigurationBuilder()
                .SetBasePath(TestContext.CurrentContext.TestDirectory)
                .AddJsonFile("appsettings.Test.json", optional: false, reloadOnChange: false)
                .Build();

            _connexionMaster = _config.GetConnectionString("MasterConnection");
            _connexionTpl = _config.GetConnectionString("TestConnection");
        }

        [SetUp]
        public void Setup()
        {
            _dbName = "purrfectMates_Test";
            _connexionTest = _connexionTpl.Replace("{DB_NAME}", _dbName);


            using (var conn = new SqlConnection(_connexionMaster))
            {
                conn.Open();
                using var cmd = new SqlCommand($"CREATE DATABASE [{_dbName}]", conn);
                cmd.ExecuteNonQuery();

            }

            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlServer(_connexionTest)
                .Options;

            _db = new AppDbContext(options);
            _db.Database.EnsureCreated();

            if (!_db.TypesAnimaux.Any())
            {
                _db.TypesAnimaux.AddRange(
                    new Models.TypeAnimal { nomTypeAnimal = "Chat" },
                    new Models.TypeAnimal { nomTypeAnimal = "Chien" });

            }

            if (!_db.TaillesAnimaux.Any())
            {
                _db.TaillesAnimaux.AddRange(
                   new TailleAnimal { nomTaille = "Petit" },
                   new TailleAnimal { nomTaille = "Moyen" }
               );
            }

            if (!_db.NiveauxActivites.Any())
            {
                _db.NiveauxActivites.AddRange(
                    new NiveauActivite { nomActivite = "Calme" },
                    new NiveauActivite { nomActivite = "Actif" }
                );
            }

            if (!_db.Utilisateurs.Any())
            {
                _db.Utilisateurs.Add(new Utilisateur
                {
                    nomUtilisateur = "Lucas",
                    prenomUtilisateur = "Josy",
                    emailUtilisateur = "josy@test.com",
                    //motDePasseUtilisateur = "MotDePasse123!",
                    Role = Role.Proprietaire
                });
            }
            _db.SaveChanges();

            // 4) instancier le controller à tester
            _controller = new PurrfectMates.Api.Controllers.AnimalsController(_db);


        }

        [TearDown]
        public void TearDown()
        {
            _db.Dispose();

            using var conn = new SqlConnection(_connexionMaster);
            conn.Open();
            var drop = $@"
                ALTER DATABASE [{_dbName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                DROP DATABASE [{_dbName}];";

            using var cmd = new SqlCommand(drop, conn);

            cmd.ExecuteNonQuery();
        }

        // TESTS

        [Test]
        public async System.Threading.Tasks.Task Create_Then_Get_Should_Work()
        {
            var userId = _db.Utilisateurs.Select(u => u.IdUtilisateur).First();
            var typeId = _db.TypesAnimaux.OrderBy(x => x.IdTypeAnimal).Select(x => x.IdTypeAnimal).First();
            var tailleId = _db.TaillesAnimaux.OrderBy(x => x.IdTailleAnimal).Select(x => x.IdTailleAnimal).First();
            var nivId = _db.NiveauxActivites.OrderBy(x => x.IdNiveauActivite).Select(x => x.IdNiveauActivite).First();


            var dto = new PurrfectMates.Api.Dtos.AnimalCreateDto
            {
                nomAnimal = "Mimine",
                race = "Européen",
                age = 2,
                IdUtilisateur = userId,
                IdNiveauActivite = nivId,
                IdTailleAnimal = tailleId,
                IdTypeAnimal = typeId,
                descriptionAnimal = "Très câline"
            };

            var created = await _controller.Create(dto, default);
            Assert.That(created.Result, Is.TypeOf<Microsoft.AspNetCore.Mvc.CreatedAtActionResult>());

            var obj = (created.Result as Microsoft.AspNetCore.Mvc.CreatedAtActionResult)!.Value as PurrfectMates.Api.Dtos.AnimalReadDto;
            Assert.That(obj, Is.Not.Null);
            Assert.That(obj!.nomAnimal, Is.EqualTo("Mimine"));

            var get = await _controller.GetOne(obj.IdAnimal, default);
            Assert.That(get.Result, Is.TypeOf<Microsoft.AspNetCore.Mvc.OkObjectResult>());
        }

        [Test]
        public async System.Threading.Tasks.Task Update_Should_Modify_Animal()
        {
            var userId = _db.Utilisateurs.Select(u => u.IdUtilisateur).First();

            // seed un animal
            var created = await _controller.Create(new PurrfectMates.Api.Dtos.AnimalCreateDto
            {
                nomAnimal = "Rex",
                race = "Berger",
                age = 1,
                IdUtilisateur = userId,
                IdNiveauActivite = 1,
                IdTailleAnimal = 2,
                IdTypeAnimal = 2,
                descriptionAnimal = "Gentil"
            }, default);

            var id = ((created.Result as Microsoft.AspNetCore.Mvc.CreatedAtActionResult)!.Value as PurrfectMates.Api.Dtos.AnimalReadDto)!.IdAnimal;

            var dto = new PurrfectMates.Api.Dtos.AnimalUpdateDto
            {
                nomAnimal = "Rexy",
                race = "Berger Allemand",
                age = 2,
                IdNiveauActivite = 2,
                IdTailleAnimal = 2,
                IdTypeAnimal = 2,
                descriptionAnimal = "Très joueur"
            };

            var resp = await _controller.Update(id, dto, default);
            Assert.That(resp, Is.TypeOf<Microsoft.AspNetCore.Mvc.NoContentResult>());

            var back = await _controller.GetOne(id, default);
            var read = (back.Result as Microsoft.AspNetCore.Mvc.OkObjectResult)!.Value as PurrfectMates.Api.Dtos.AnimalReadDto;
            Assert.That(read!.nomAnimal, Is.EqualTo("Rexy"));
            Assert.That(read.age, Is.EqualTo(2));
        }

        [Test]
        public async System.Threading.Tasks.Task Delete_Should_Remove_Animal()
        {
            var userId = _db.Utilisateurs.Select(u => u.IdUtilisateur).First();

            var created = await _controller.Create(new PurrfectMates.Api.Dtos.AnimalCreateDto
            {
                nomAnimal = "Luna",
                race = "Siamois",
                age = 3,
                IdUtilisateur = userId,
                IdNiveauActivite = 1,
                IdTailleAnimal = 1,
                IdTypeAnimal = 1,
                descriptionAnimal = "Ok"
            }, default);

            var id = ((created.Result as Microsoft.AspNetCore.Mvc.CreatedAtActionResult)!.Value as PurrfectMates.Api.Dtos.AnimalReadDto)!.IdAnimal;

            var del = await _controller.Delete(id, default);
            Assert.That(del, Is.TypeOf<Microsoft.AspNetCore.Mvc.NoContentResult>());

            var after = await _controller.GetOne(id, default);
            Assert.That(after.Result, Is.TypeOf<Microsoft.AspNetCore.Mvc.NotFoundResult>());
        }

    }
}