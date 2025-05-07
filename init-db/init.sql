DROP TABLE IF EXISTS nombre_analyse;

CREATE TABLE nombre_analyse (
    Id INT PRIMARY KEY AUTO_INCREMENT,
    Nombre INT NOT NULL,
    EstPair BOOLEAN NOT NULL,
    EstParfait BOOLEAN NOT NULL,
    EstPremier BOOLEAN NOT NULL
);
