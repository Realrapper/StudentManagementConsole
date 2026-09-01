SELECT * FROM dbo.vw_StudentResults; 
EXEC dbo.usp_GetStudentResults 1;
UPDATE dbo.Enrolment SET FinalMark=80 WHERE StudentID=1 AND CourseID=1; 
SELECT * FROM dbo.MarkAudit;