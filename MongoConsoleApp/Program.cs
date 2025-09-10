using MongoDB.Bson;
using MongoDB.Driver;
using System;
using System.Net;
using System.Text;
using System.Web;

public class User
{
    public ObjectId Id { get; set; }
    public string Name { get; set; }
    public string Surname { get; set; }
    public string IdNumber { get; set; }
    public string DateOfBirth { get; set; }
}

class Program
{
    static void Main(string[] args)
    {
        // 1. Start a tiny web server on localhost:5000
        HttpListener listener = new HttpListener();
        listener.Prefixes.Add("http://localhost:5000/");
        listener.Start();
        Console.WriteLine("Listening on http://localhost:5000 ...");

        // 2. Connect to MongoDB
        var client = new MongoClient("mongodb://localhost:27017");
        var database = client.GetDatabase("TestDB");
        var collection = database.GetCollection<User>("Users");

        // 3. Keep listening for requests
        while (true)
        {
            var context = listener.GetContext();    // Wait for browser request
            var request = context.Request;
            var response = context.Response;

            if (request.HttpMethod == "POST" && request.Url.AbsolutePath == "/submit")
            {
                // 4. Read form data (sent from HTML page)
                string body;
                using (var reader = new System.IO.StreamReader(request.InputStream, request.ContentEncoding))
                {
                    body = reader.ReadToEnd();
                }

                var data = HttpUtility.ParseQueryString(body);
                string name = data["name"];
                string surname = data["surname"];
                string idNumber = data["idNumber"];
                string dob = data["dob"];

                string message = "";

                // 5. Validation
                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(surname))
                {
                    message = "Error: Name and Surname are required.";
                }
                else if (idNumber.Length != 13 || !long.TryParse(idNumber, out _))
                {
                    message = "Error: ID Number must be 13 digits.";
                }
                else if (!System.Text.RegularExpressions.Regex.IsMatch(dob, @"^\d{2}/\d{2}/\d{4}$"))
                {
                    message = "Error: Date must be dd/mm/yyyy.";
                }
                else if (collection.Find(u => u.IdNumber == idNumber).Any())
                {
                    message = "Error: Duplicate ID Number.";
                }
                else
                {
                    // 6. Save to MongoDB
                    var user = new User { Name = name, Surname = surname, IdNumber = idNumber, DateOfBirth = dob };
                    collection.InsertOne(user);
                    message = "Success: User saved!";
                }

                // 7. Send response back to browser
                byte[] buffer = Encoding.UTF8.GetBytes(message);
                response.ContentLength64 = buffer.Length;
                response.OutputStream.Write(buffer, 0, buffer.Length);
                response.OutputStream.Close();
            }
        }
    }
}
