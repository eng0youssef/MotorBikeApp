-- =================================================================================
-- ”ﬂ—Ì»  „‰Õ ﬂ«›… «·’·«ÕÌ«  ··„” Œœ„ «·„”ƒÊ· (UserID = 0)
-- Ì—ÃÏ  ‘€Ì· Â–« «·”ﬂ—Ì»  ⁄·Ï ﬁ«⁄œ… »Ì«‰«  «·⁄„Ì· »⁄œ  ÕœÌÀ «·‰Ÿ«„
-- =================================================================================

BEGIN TRANSACTION;

BEGIN TRY
    DECLARE @UserID INT = 0;

    -- „”Õ «·’·«ÕÌ«  «·ﬁœÌ„… ·Â–« «·„” Œœ„ · Ã‰» «· ﬂ—«— √Ê «·”Ã·«  «·Œ«ÿ∆…
    DELETE FROM [User_Sub] WHERE UserID = @UserID;

    -- ÃœÊ· „ƒﬁ  · Œ“Ì‰ √—ﬁ«„ «·‘«‘«  (FrmID) »‰«¡ ⁄·Ï „·› ScreenId.cs
    DECLARE @Screens TABLE (FrmID INT);
    INSERT INTO @Screens (FrmID) VALUES 
        (1),  (2),  (3),  (4),  (5),  (6),  (7),  (8),  (9),  (10), -- «·»Ì«‰«  «·√”«”Ì…
        (11), (12), (13), (14), (15), (16), (17),                   -- «·»Ì«‰«  «·√”«”Ì…
        (20), (21), (22), (23),                                     -- «·›Ê« Ì—
        (30), (31),                                                 -- «·„— Ã⁄« 
        (40), (41), (42), (43),                                     -- «·„œ›Ê⁄«  Ê«· ÕÊÌ·« 
        (50), (51),                                                 -- √Œ—Ï
        (60), (61), (62), (63),                                     -- «·«” Ì—«œ
        (70), (71), (72), (73), (74), (75), (76),                   -- «· ﬁ«—Ì—
        (80);                                                       -- «·≈⁄œ«œ« 

    -- «·Õ’Ê· ⁄·Ï √⁄·Ï IDSub ·÷„«‰ ⁄œ„ ÕœÊÀ  ⁄«—÷ (Primary Key)
    DECLARE @MaxID INT;
    SELECT @MaxID = ISNULL(MAX(IDSub), 0) FROM [User_Sub];

    -- ≈œŒ«· ﬂ«›… «·‘«‘«  »’·«ÕÌ«  „Ã„⁄… (View,Add,Edit,Delete) ›Ì ”Ã· Ê«Õœ ·ﬂ· ‘«‘…
    INSERT INTO [User_Sub] (IDSub, UserID, FrmID, Ability)
    SELECT 
        @MaxID + ROW_NUMBER() OVER (ORDER BY s.FrmID),
        @UserID,
        s.FrmID,
        'View,Add,Edit,Delete'
    FROM @Screens s;

    COMMIT TRANSACTION;
    PRINT ' „ »‰Ã«Õ „‰Õ ﬂ«›… «·’·«ÕÌ«  ··„” Œœ„ «·„”ƒÊ· (UserID = 0).';

END TRY
BEGIN CATCH
    ROLLBACK TRANSACTION;
    PRINT 'ÕœÀ Œÿ√ √À‰«¡  ‰›Ì– «·”ﬂ—Ì» :';
    PRINT ERROR_MESSAGE();
END CATCH;

