CREATE INDEX idx_dataobject_objecttype ON DataObject (`ObjectType`);
CREATE INDEX idx_dataobject_name ON DataObject (`Name`);
CREATE INDEX idx_dataobject_objecttype_name ON DataObject (`ObjectType`, `Name`);
CREATE INDEX idx_dataobject_attributes_dataobjectid ON DataObject_Attributes (`DataObjectId`);
CREATE INDEX idx_dataobject_attributes_attributename ON DataObject_Attributes (`AttributeName`);
CREATE INDEX idx_signaturemap_dataobjectid_typeid ON DataObject_SignatureMap (`DataObjectId`, `DataObjectTypeId`);
CREATE INDEX idx_publishers_publisher ON Signatures_Publishers (`Publisher`);
CREATE INDEX idx_platforms_platform ON Signatures_Platforms (`Platform`);
CREATE INDEX idx_signatures_roms_gameid_name ON Signatures_Roms (`GameId`, `Name`);
