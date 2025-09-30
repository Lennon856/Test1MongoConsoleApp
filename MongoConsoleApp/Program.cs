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

        // Connect to MongoDB
        var client = new MongoClient("mongodb://localhost:27017");
        var database = client.GetDatabase("TestDB");
        var collection = database.GetCollection<User>("Users");

        while (true)
        {
            var context = listener.GetContext();
            var request = context.Request;
            var response = context.Response;

            string html;

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

                string errorMsg = "";
                string successMsg = "";

                // Validation
                if (string.IsNullOrWhiteSpace(name))
                    errorMsg = "Name is required.";
                else if (string.IsNullOrWhiteSpace(surname))
                    errorMsg = "Surname is required.";
                else if (idNumber.Length != 13 || !long.TryParse(idNumber, out _))
                    errorMsg = "ID Number must be exactly 13 digits.";
                else if (!DateTime.TryParseExact(dob.Trim(), "dd/MM/yyyy", null,
                         System.Globalization.DateTimeStyles.None, out DateTime parsedDob))
                    errorMsg = "Date must be dd/MM/yyyy format.";
                else if (collection.Find(u => u.IdNumber == idNumber).Any())
                    errorMsg = "Duplicate ID Number.";
                else
                {
                    var user = new User
                    {
                        Name = name,
                        Surname = surname,
                        IdNumber = idNumber,
                        DateOfBirth = parsedDob.ToString("dd/MM/yyyy")
                    };
                    collection.InsertOne(user);
                    successMsg = "Data saved successfully!";
                }

                html = RenderForm(name, surname, idNumber, dob, errorMsg, successMsg);
            }
            else
            {
                // Default page load
                html = RenderForm("", "", "", "", "", "");
            }

            // Return HTML response
            byte[] buffer = Encoding.UTF8.GetBytes(html);
            response.ContentType = "text/html; charset=UTF-8";
            response.ContentLength64 = buffer.Length;
            using (var output = response.OutputStream)
            {
                output.Write(buffer, 0, buffer.Length);
            }
        }
    }

    // Render styled form with error/success messages
    private static string RenderForm(string name, string surname, string idNumber, string dob, string error = "", string success = "")
    {
        return $@"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"" />
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"" />
    <title>User Input Form</title>
    <style>
        body {{
            background: #f7f7f7;
            font-family: Arial, sans-serif;
        }}
        .container {{
            width: 400px;
            margin: 50px auto;
            background: #ffffff;
            border-radius: 8px;
            box-shadow: 0 0 10px rgba(0,0,0,0.1);
            padding: 20px;
        }}
        .container h2 {{
            text-align: center;
            color: #333;
        }}
        label {{
            display: block;
            margin-bottom: 5px;
            font-weight: bold;
            color: #333;
        }}
        input[type=text] {{
            width: 100%;
            padding: 8px;
            margin-bottom: 15px;
            border: 1px solid #ccc;
            border-radius: 4px;
            box-sizing: border-box;
            transition: border-color 0.3s, box-shadow 0.3s;
        }}
        input[type=text]:focus {{
            outline: 3px solid #FFCC00;
            box-shadow: 0 0 10px #FFCC00;
            border-color: #FFCC00;
        }}
        input[name=dob] {{
            background-color: #FFFBCC;
        }}
        .error {{
            color: red;
            margin-bottom: 10px;
            font-size: 13px;
        }}
        .success {{
            color: green;
            margin-bottom: 10px;
        }}
        .buttons {{
            text-align: center;
            margin-top: 10px;
        }}
        .buttons button {{
            padding: 10px 20px;
            border: none;
            border-radius: 4px;
            color: white;
            font-size: 14px;
            cursor: pointer;
            margin: 0 5px;
        }}
        .buttons .post {{
            background-color: #4CAF50;
        }}
        .buttons .cancel {{
            background-color: #FA8072;
        }}
        .buttons button:hover {{
            opacity: 0.9;
        }}
    </style>
</head>
<body>
    <div class=""container"">
        <h2>User Information</h2>

        {(string.IsNullOrEmpty(success) ? "" : $"<div class='success'>{success}</div>")}
        {(string.IsNullOrEmpty(error) ? "" : $"<div class='error'>{error}</div>")}

        <form method=""post"" action=""/submit"">
            <label>Name:</label>
            <input type=""text"" name=""name"" required value=""{WebUtility.HtmlEncode(name)}"" />

            <label>Surname:</label>
            <input type=""text"" name=""surname"" required value=""{WebUtility.HtmlEncode(surname)}"" />

            <label>ID Number:</label>
            <input type=""text"" name=""idNumber"" maxlength=""13"" pattern=""\d{{13}}"" 
                   title=""Must be exactly 13 digits"" required value=""{WebUtility.HtmlEncode(idNumber)}"" />

            <label>Date of Birth (dd/MM/yyyy):</label>
            <input type=""text"" name=""dob"" placeholder=""dd/MM/yyyy""
                   pattern=""\d{{2}}/\d{{2}}/\d{{4}}"" 
                   title=""Format: dd/MM/yyyy"" required value=""{WebUtility.HtmlEncode(dob)}"" />

            <div class=""buttons"">
                <button type=""submit"" class=""post"">POST</button>
                <button type=""button"" class=""cancel"" onclick=""window.location.href='/'"">CANCEL</button>
            </div>
        </form>
    </div>
</body>
</html>";
    }
}
