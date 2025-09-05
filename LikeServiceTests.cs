using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PurrfectMates.Api.Data;
using PurrfectMates.Api.Services;
using PurrfectMates.Enums;
using PurrfectMates.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace PurrfectMates.Tests
{
    [TestFixture]
    public class LikeServiceTests
    {
        // ici on a les attributs privés
        private IConfiguration _config = default!;
        private string _dbName = default!;
        private string _connexionMaster = default!;
        private string _connexionTest = default!;
        private string _connexionTpl = default!;
        private AppDbContext _db = default!;//je definis les champs privés pour utiliser dans les test la bdd de test 
        private LikeService _service = default!; // le service qu'on teste


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
            _dbName = "purrfectMates_LikeServiceTest";
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

            // Initialiser LikeService avec ce DbContext
            _service = new LikeService(_db);

            // Seed de base : 1 utilisateur et 1 animal
            var user = new Utilisateur
            {
                nomUtilisateur = "Test",
                prenomUtilisateur = "User",
                emailUtilisateur = "test@demo.com",
                motDePasseUtilisateurHash = "hash",
                Role = Role.Adoptant
            };

            var animal = new Animal
            {
                nomAnimal = "Minou",
                race = "Chartreux",
                age = 2,
                descriptionAnimal = "Gentil"
            };

            _db.Utilisateurs.Add(user);
            _db.Animaux.Add(animal);
            _db.SaveChanges();
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


        [Test]
        public async Task Like_Should_Create_Swipe_And_Match()
        {
            // Arrange : je récupère les id de l’utilisateur et de l’animal créés dans Setup
            var userId = _db.Utilisateurs.First().IdUtilisateur;
            var animalId = _db.Animaux.First().IdAnimal;

            // Act : j’appelle mon service avec "like"
            var like = await _service.AjouterLikeAsync(userId, animalId, "like");

            // Assert : je vérifie qu’un swipe est bien créé en base
            var swipeInDb = _db.Likes.FirstOrDefault(l => l.idUtilisateur == userId && l.idAnimal == animalId);
            Assert.That(swipeInDb, Is.Not.Null); // il doit exister
            Assert.That(swipeInDb!.actionSwipe, Is.EqualTo("like")); // et l’action doit être "like"

            // Assert : je vérifie qu’un match est aussi créé
            var matchInDb = _db.Matches.FirstOrDefault(m => m.UtilisateurId == userId && m.AnimalId == animalId);
            Assert.That(matchInDb, Is.Not.Null); // il doit exister
            Assert.That(matchInDb!.EstAime, Is.True); // il doit être à "true"
        }


    }

}
