using MongoDB.Bson;
using MongoDB.Driver;
using System;
using System.Net;
using System.Text;
using System.Web;
using System.Text.RegularExpressions;

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
        // Start web server
        HttpListener listener = new HttpListener();
        listener.Prefixes.Add("http://localhost:5000/");
        listener.Start();
        Console.WriteLine("Listening on http://localhost:5000 ...");

        // Connect to Mongo
        var client = new MongoClient("mongodb://localhost:27017");
        var database = client.GetDatabase("TestDB");
        var collection = database.GetCollection<User>("Users");

        while (true)
        {
            var context = listener.GetContext();
            var request = context.Request;
            var response = context.Response;

            if (request.HttpMethod == "POST" && request.Url.AbsolutePath == "/submit")
            {
                string body;
                using (var reader = new System.IO.StreamReader(request.InputStream, request.ContentEncoding))
                    body = reader.ReadToEnd();

                var data = HttpUtility.ParseQueryString(body);
                string name = data["name"] ?? "";
                string surname = data["surname"] ?? "";
                string idNumber = data["idNumber"] ?? "";
                string dob = data["dob"] ?? "";

                string html;

                // Validation
                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(surname))
                {
                    html = RenderForm(name, surname, idNumber, dob, "Error: Name and Surname are required.");
                }
                else if (idNumber.Length != 13 || !long.TryParse(idNumber, out _))
                {
                    html = RenderForm(name, surname, idNumber, dob, "Error: ID Number must be 13 digits.");
                }
                else if (!Regex.IsMatch(dob, @"^\d{2}/\d{2}/\d{4}$"))
                {
                    html = RenderForm(name, surname, idNumber, dob, "Error: Date must be dd/mm/yyyy.");
                }
                else if (collection.Find(u => u.IdNumber == idNumber).Any())
                {
                    html = RenderForm(name, surname, idNumber, dob, "Error: Duplicate ID Number.");
                }
                else
                {
                    var user = new User { Name = name, Surname = surname, IdNumber = idNumber, DateOfBirth = dob };
                    collection.InsertOne(user);
                    html = RenderForm("", "", "", "", "Success: User saved!");
                }

                // Return HTML response
                byte[] buffer = Encoding.UTF8.GetBytes(html); // Convert HTML text into bytes
                response.ContentType = "text/html";
                response.ContentLength64 = buffer.Length;
                response.OutputStream.Write(buffer, 0, buffer.Length);
                response.OutputStream.Close();
            }
            else
            {
                // Fallback: serve default form if accessed directly
                string html = RenderForm("", "", "", "");
                byte[] buffer = Encoding.UTF8.GetBytes(html);
                response.ContentType = "text/html";
                response.ContentLength64 = buffer.Length;
                response.OutputStream.Write(buffer, 0, buffer.Length);
                response.OutputStream.Close();
            }
        }
    }
    // This method builds and returns the HTML form as a string
    private static string RenderForm(string name, string surname, string idNumber, string dob, string message = "")
    {
        return $@"<!DOCTYPE html>
<html>
<body>
    <h2>Capture User Details</h2>

    {(string.IsNullOrEmpty(message) ? "" : $"<p style='color:red'>{WebUtility.HtmlEncode(message)}</p>")}

    <form action=""/submit"" method=""post"">
        <label for=""name"">Name:</label><br>
        <input type=""text"" id=""name"" name=""name"" value=""{WebUtility.HtmlEncode(name)}""><br>

        <label for=""surname"">Surname:</label><br>
        <input type=""text"" id=""surname"" name=""surname"" value=""{WebUtility.HtmlEncode(surname)}""><br>

        <label for=""idNumber"">ID Number:</label><br>
        <input id=""idNumber"" type=""text"" name=""idNumber"" maxlength=""13""
               pattern=""\d{{13}}"" title=""Must be exactly 13 digits"" required
               value=""{WebUtility.HtmlEncode(idNumber)}""><br>

        <label for=""dob"">Date of Birth(dd/mm/yyyy):</label><br>
        <input id=""dob"" type=""text"" name=""dob"" pattern=""\d{{2}}/\d{{2}}/\d{{4}}""
               title=""Format:dd/mm/yyyy"" required
               value=""{WebUtility.HtmlEncode(dob)}""><br><br>

        <input type=""submit"" value=""POST"">
        <input type=""reset"" value=""CANCEL"">
    </form>
</body>
</html>";
    }
}
