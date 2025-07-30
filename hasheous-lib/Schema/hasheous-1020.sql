CREATE INDEX idx_games_name_systemid_publisherid ON Signatures_Games (Name, SystemId, PublisherId);

ALTER TABLE Signatures_Games ADD FULLTEXT INDEX ft_name (Name);