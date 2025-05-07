using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Minio;
using Minio.DataModel.Args;
using Minio.Exceptions;
using System.Text;
using System.IO;
using Newtonsoft.Json;


namespace IntermediaryApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class NumberController : ControllerBase
    {
        private readonly string _connectionString;
        private readonly HttpClient _httpClient;
        private readonly ILogger<NumberController> _logger;
        private readonly IMinioClient _minioClient;
        private readonly string _bucketName = "syracuse";

        public NumberController(IConfiguration configuration, HttpClient httpClient, ILogger<NumberController> logger)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection") ?? throw new ArgumentNullException(nameof(configuration));
            _httpClient = httpClient;
            _logger = logger;
            _Client = new MinioClient()
                .WithEndpoint("s3:9000")
                .WithCredentials(MINIO_ROOT_USER, MINIO_ROOT_PASSWORD)
                .WithSSL(false)
                .Build();

            EnsureBucketExistsAsync().GetAwaiter().GetResult();
        }

        private async Task EnsureBucketExistsAsync()
        {
            try
            {
                var args = new Minio.DataModel.Args.BucketExistsArgs()
                    .WithBucket(_bucketName);

                bool bucketExists = await _minioClient.BucketExistsAsync(args);

                if (!bucketExists)
                {
                    var makeBucketArgs = new Minio.DataModel.Args.MakeBucketArgs()
                        .WithBucket(_bucketName);

                    await _minioClient.MakeBucketAsync(makeBucketArgs);
                    _logger.LogInformation($"Bucket '{_bucketName}' créé avec succès.");
                }
                else
                {
                    _logger.LogInformation($"Le bucket '{_bucketName}' existe déjà.");
                }
            }
            catch (MinioException e)
            {
                _logger.LogError($"Erreur lors de la gestion du bucket: {e.Message}");
                throw;
            }
        }

        public class NumberResult
        {
            public int Nombre { get; set; }
            public bool EstPair { get; set; }
            public bool EstParfait { get; set; }
            public bool EstPremier { get; set; }
        }

        public class SyracuseResult
        {
            public int Nombre { get; set; }
            public string? SuiteSyracuse { get; set; } 
        }

        public class InsertNumberRequest
        {
            public NumberResult? Data { get; set; }
            public SyracuseResult? SyracuseData { get; set; }
        }

        public class CombinedData
        {
            public MySQLData? MySQLData { get; set; }
            public SyracuseData? SyracuseData { get; set; }
        }

        public class MySQLData
        {
            public int Nombre { get; set; }
            public bool EstPair { get; set; }
            public bool EstParfait { get; set; }
            public bool EstPremier { get; set; }
        }

        public class SyracuseData
        {
            public int Nombre { get; set; }
            public List<int> SuiteSyracuse { get; set; } = new List<int>(); 
        }


        [HttpGet("verify-id")]
        public async Task<ActionResult<object>> VerifyNumber(int nombre)
        {
            var mysqlTask = VerifyNumberInMySQL(nombre);
            var minioTask = VerifyNumberInMinIO(nombre);

            await Task.WhenAll(mysqlTask, minioTask);

            var mysqlResult = await mysqlTask;
            var syracuseSequence = await minioTask;

            _logger.LogInformation($"Vérification de {nombre} : MySQL - Existe: {mysqlResult.Exists}, Pair: {mysqlResult.EstPair}, Parfait: {mysqlResult.EstParfait}, Premier: {mysqlResult.EstPremier}");
            _logger.LogInformation($"Données Syracuse pour {nombre} : {syracuseSequence}");

            return Ok(new
            {
                mysqlResult.Exists,
                mysqlResult.EstPair,
                mysqlResult.EstParfait,
                mysqlResult.EstPremier,
                Syracuse = syracuseSequence
            });
        }

        private async Task<(bool Exists, bool EstPair, bool EstParfait, bool EstPremier)> VerifyNumberInMySQL(int nombre)
        {
            using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            var command = new MySqlCommand("SELECT * FROM nombre_analyse WHERE Nombre = @nombre", connection);
            command.Parameters.AddWithValue("@nombre", nombre);

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return (
                    true,
                    reader.GetBoolean(reader.GetOrdinal("EstPair")),
                    reader.GetBoolean(reader.GetOrdinal("EstParfait")),
                    reader.GetBoolean(reader.GetOrdinal("EstPremier"))
                );
            }
            return (false, false, false, false);
        }

        private async Task<object?> VerifyNumberInMinIO(int nombre)
        {
            try
            {
                var objectName = $"syracuse_{nombre}.json";
                _logger.LogInformation($"Tentative de récupération de l'objet Syracuse pour le nombre {nombre} avec le nom de fichier {objectName}");

                using var stream = new MemoryStream();

                var getObjectArgs = new GetObjectArgs()
                    .WithBucket(_bucketName)
                    .WithObject(objectName)
                    .WithCallbackStream(async (dataStream) =>
                    {
                        await dataStream.CopyToAsync(stream);
                    });

                _logger.LogInformation($"Envoi de la requête pour récupérer l'objet {objectName} de MinIO...");

                await _minioClient.GetObjectAsync(getObjectArgs);

                _logger.LogInformation($"Objet {objectName} récupéré avec succès depuis MinIO.");

                stream.Seek(0, SeekOrigin.Begin);

                using var reader = new StreamReader(stream);
                var content = await reader.ReadToEndAsync();

                if (string.IsNullOrWhiteSpace(content))
                {
                    _logger.LogWarning($"Aucune donnée Syracuse trouvée pour le nombre {nombre}. Contenu du fichier est vide.");
                    return null; 
                }

                _logger.LogInformation($"Contenu de l'objet Syracuse pour {nombre} récupéré avec succès : {content.Substring(0, Math.Min(100, content.Length))}...");

                var syracuseData = JsonConvert.DeserializeObject<List<int>>(content);

                if (syracuseData == null)
                {
                    _logger.LogError($"Impossible de désérialiser les données Syracuse pour le nombre {nombre}. Le contenu JSON est malformé.");
                    return null;
                }

                _logger.LogInformation($"Données Syracuse pour le nombre {nombre} désérialisées avec succès.");
                return syracuseData;
            }
            catch (MinioException ex)
            {
                _logger.LogError($"Erreur lors de la récupération des données Syracuse pour le nombre {nombre}: {ex.Message}");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Erreur inattendue lors de la récupération des données Syracuse pour {nombre}: {ex.Message}");
                return null;
            }
        }


        [HttpPost("insert")]
        public async Task<ActionResult<object>> InsertData([FromBody] CombinedData combinedData)
        {
            try
            {
                var mysqlData = combinedData.MySQLData;
                var syracuseData = combinedData.SyracuseData;

                var mysqlInsertResult = await InsertMySQLData(mysqlData);
                if (mysqlInsertResult == null)
                {
                    return BadRequest("Échec de l'insertion des données MySQL.");
                }

                var syracuseInsertResult = await InsertSyracuseData(syracuseData);
                if (syracuseInsertResult == null)
                {
                    return BadRequest("Échec de l'insertion des données Syracuse.");
                }

                return Ok(new
                {
                    mysqlResult = mysqlInsertResult,
                    syracuseResult = syracuseInsertResult
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Erreur lors de l'insertion : {ex.Message}");
                return StatusCode(500, new { error = "Erreur interne du serveur", details = ex.Message });
            }
        }

        private async Task<object> InsertMySQLData(MySQLData data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            try
            {
                using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();

                var command = new MySqlCommand("INSERT INTO nombre_analyse (Nombre, EstPair, EstParfait, EstPremier) VALUES (@Nombre, @EstPair, @EstParfait, @EstPremier)", connection);
                command.Parameters.AddWithValue("@Nombre", data.Nombre);
                command.Parameters.AddWithValue("@EstPair", data.EstPair);
                command.Parameters.AddWithValue("@EstParfait", data.EstParfait);
                command.Parameters.AddWithValue("@EstPremier", data.EstPremier);

                var result = await command.ExecuteNonQueryAsync();
                
                return result > 0 ? new { Message = "Insertion réussie dans MySQL", Data = data } : null;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Erreur lors de l'insertion dans MySQL : {ex.Message}");
                return null;
            }
        }

        private async Task<object?> InsertSyracuseData(SyracuseData data)
        {
            if (data == null) return null;
            try
            {
                var objectName = $"syracuse_{data.Nombre}.json";
                var jsonData = JsonConvert.SerializeObject(data.SuiteSyracuse);
                var stream = new MemoryStream(Encoding.UTF8.GetBytes(jsonData));

                await _minioClient.PutObjectAsync(new PutObjectArgs()
                    .WithBucket(_bucketName)
                    .WithObject(objectName)
                    .WithStreamData(stream)
                    .WithObjectSize(stream.Length));

                return new { Message = "Insertion réussie dans MinIO", Data = data };
            }
            catch (Exception ex)
            {
                _logger.LogError($"Erreur lors de l'insertion dans MinIO : {ex.Message}");
                return null;
            }
        }
    }
}

