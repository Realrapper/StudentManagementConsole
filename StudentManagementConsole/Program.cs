using Microsoft.Data.SqlClient;
using System.Globalization;

class Program
{
    static readonly string? Cs = Environment.GetEnvironmentVariable("STUDENT_DB_CONNECTION");
    static void Main()
    {
        if (string.IsNullOrWhiteSpace(Cs)) { Console.WriteLine("Set STUDENT_DB_CONNECTION before running. See guide."); return; }
        while (true)
        {
            Console.WriteLine("\nSTUDENT MANAGEMENT SYSTEM\n1 Display students\n2 Search student\n3 Register student\n4 Enrol student\n5 Capture/update mark\n6 View results\n7 Students without enrolments\n8 Record payment\n9 Exit");
            try { switch (Prompt("Choose 1-9: ")) { case "1": DisplayStudents(); break; case "2": Search(); break; case "3": Register(); break; case "4": Enrol(); break; case "5": Mark(); break; case "6": Results(); break; case "7": Unenrolled(); break; case "8": Payment(); break; case "9": return; default: Console.WriteLine("Enter a number from 1 to 9."); break; } }
            catch (SqlException ex) { Console.WriteLine($"Database operation failed: {FriendlySqlMessage(ex)}"); }
            catch (Exception ex) { Console.WriteLine($"Could not complete that action: {ex.Message}"); }
        }
    }
    static SqlConnection Conn() => new(Cs!);
    static string Prompt(string text) { Console.Write(text); return Console.ReadLine()?.Trim() ?? ""; }
    static string Required(string text)
    {
        while (true)
        {
            string value = Prompt(text);
            if (!string.IsNullOrWhiteSpace(value)) return value;
            Console.WriteLine("This field is required.");
        }
    }
    static int ReadInt(string text) { while (true) if (int.TryParse(Prompt(text), out var x) && x > 0) return x; else Console.WriteLine("Enter a positive whole number."); }
    static decimal MarkValue() { while (true) if (decimal.TryParse(Prompt("Mark (0-100): "), NumberStyles.Number, CultureInfo.InvariantCulture, out var x) && x >= 0 && x <= 100) return x; else Console.WriteLine("Enter a number from 0 to 100."); }
    static void Execute(string sql, params SqlParameter[] p) { using var c = Conn(); using var cmd = new SqlCommand(sql, c); cmd.Parameters.AddRange(p); c.Open(); Console.WriteLine(cmd.ExecuteNonQuery() == 1 ? "Saved successfully." : "No matching record was found."); }
    static void Print(SqlCommand cmd) { using var c = Conn(); cmd.Connection = c; c.Open(); using var r = cmd.ExecuteReader(); if (!r.HasRows) { Console.WriteLine("No records found."); return; } for (int i = 0; i < r.FieldCount; i++) Console.Write($"{r.GetName(i),-18}"); Console.WriteLine(); while (r.Read()) { for (int i = 0; i < r.FieldCount; i++) Console.Write($"{(r.IsDBNull(i) ? "(not recorded)" : r.GetValue(i)),-18}"); Console.WriteLine(); } }
    static void DisplayStudents() { Print(new SqlCommand("SELECT StudentID,StudentNumber,FullName,Email,Status FROM dbo.Student ORDER BY StudentID")); }
    static void Search() { var n = Required("Student number: "); var cmd = new SqlCommand("SELECT StudentID,StudentNumber,FullName,Email,Status FROM dbo.Student WHERE StudentNumber=@n"); cmd.Parameters.Add("@n", System.Data.SqlDbType.VarChar, 20).Value = n; Print(cmd); }
    static void Register() { var no = Required("Student number: "); var name = Required("Full name: "); var email = Required("Email: "); if (!email.Contains('@')) throw new ArgumentException("Enter a valid email address."); Execute("INSERT dbo.Student(StudentNumber,FullName,Email,Status) VALUES(@no,@name,@email,'Inactive')", new("@no", System.Data.SqlDbType.VarChar, 20) { Value = no }, new("@name", System.Data.SqlDbType.NVarChar, 100) { Value = name }, new("@email", System.Data.SqlDbType.NVarChar, 255) { Value = email }); }
    static void Enrol() { int s = ReadInt("Student ID: "), co = ReadInt("Course ID: "); Execute("BEGIN TRY BEGIN TRANSACTION; INSERT dbo.Enrolment(StudentID,CourseID,EnrolmentDate) VALUES(@s,@c,CAST(GETDATE() AS date)); UPDATE dbo.Student SET Status='Active' WHERE StudentID=@s; COMMIT; END TRY BEGIN CATCH IF @@TRANCOUNT>0 ROLLBACK; THROW; END CATCH", new("@s", System.Data.SqlDbType.Int) { Value = s }, new("@c", System.Data.SqlDbType.Int) { Value = co }); }
    static void Mark() { int s = ReadInt("Student ID: "), c = ReadInt("Course ID: "); Execute("UPDATE dbo.Enrolment SET FinalMark=@m WHERE StudentID=@s AND CourseID=@c", new("@m", System.Data.SqlDbType.Decimal) { Precision = 5, Scale = 2, Value = MarkValue() }, new("@s", System.Data.SqlDbType.Int) { Value = s }, new("@c", System.Data.SqlDbType.Int) { Value = c }); }
    static void Results() { var cmd = new SqlCommand("dbo.usp_GetStudentResults") { CommandType = System.Data.CommandType.StoredProcedure }; cmd.Parameters.Add("@StudentID", System.Data.SqlDbType.Int).Value = ReadInt("Student ID: "); Print(cmd); }
    static void Unenrolled() { Print(new SqlCommand("SELECT s.StudentID,s.StudentNumber,s.FullName FROM dbo.Student s LEFT JOIN dbo.Enrolment e ON e.StudentID=s.StudentID WHERE e.StudentID IS NULL")); }
    static void Payment() { int s = ReadInt("Student ID: "); decimal amount; while (!decimal.TryParse(Prompt("Amount: "), out amount) || amount <= 0) Console.WriteLine("Amount must be greater than zero."); var reference = Required("Reference number: "); Execute("INSERT dbo.Payment(StudentID,Amount,PaymentDate,ReferenceNumber) VALUES(@s,@a,CAST(GETDATE() AS date),@r)", new("@s", System.Data.SqlDbType.Int) { Value = s }, new("@a", System.Data.SqlDbType.Decimal) { Precision = 10, Scale = 2, Value = amount }, new("@r", System.Data.SqlDbType.VarChar, 30) { Value = reference }); }
    static string FriendlySqlMessage(SqlException ex) => ex.Number is 2627 or 2601 ? "That value already exists; use a unique student number, email, or reference." : ex.Number == 547 ? "The value violates a rule or refers to a record that does not exist." : "Please check the values and try again.";
}
