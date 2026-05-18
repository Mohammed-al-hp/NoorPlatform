UPDATE AspNetUsers 
SET PasswordHash = 'AQAAAAEAACcQAAAAEBLf6U0F+L1XoX4p8p6w7j8j5k9l0m1n2o3p4q5r6s7t8u9v0w==' 
WHERE Email = 'student@noor.sa';
SELECT Id, UserName, Email, NormalizedEmail 
FROM AspNetUsers;