-- Synthetic complete history, used only in exclusively owned integration/E2E catalogs.
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;
DECLARE @ticket uniqueidentifier='60000000-0000-0000-0000-000000000001';
IF EXISTS(SELECT 1 FROM dbo.Tickets WHERE Id=@ticket) BEGIN COMMIT; RETURN; END;
INSERT dbo.CategoryReferences(Id,DisplayName,IsAvailable) VALUES
 ('40000000-0000-0000-0000-000000000001',N'Current history category one',0),
 ('40000000-0000-0000-0000-000000000002',N'Current history category two',0);
INSERT dbo.AgentReferences(Id,Issuer,Subject,DisplayName,IsEligible) VALUES
 ('50000000-0000-0000-0000-000000000001',N'https://example.test',N'history-agent-one',N'Current history agent one',0),
 ('50000000-0000-0000-0000-000000000002',N'https://example.test',N'history-agent-two',N'Current history agent two',0);
INSERT dbo.Tickets(Id,Reference,Subject,Description,CategoryId,Priority,Status,AssignedAgentId,CreatedAtUtc,UpdatedAtUtc,Revision)
VALUES(@ticket,N'TKT-60000000000000000000000000000001',N'Current history ticket',N'تفاصيل جديدة'+NCHAR(10)+N' محفوظة',
 '40000000-0000-0000-0000-000000000002',N'High',N'Open','50000000-0000-0000-0000-000000000001',
 '2026-10-08T09:00:00+00:00','2026-10-08T09:00:00+00:00',9007199254740993);
DECLARE @entries TABLE(Ordinal int,Revision bigint,Sequence int,Kind nvarchar(32),Field nvarchar(32),PreviousValue nvarchar(max),NewValue nvarchar(max));
INSERT @entries VALUES
 (1,1,1,N'Created',NULL,NULL,N'{"reference":"TKT-60000000000000000000000000000001","subject":"History original subject","description":"وصف قديم","category":{"id":"40000000-0000-0000-0000-000000000001","displayName":"History category one"},"priority":"Normal","status":"Open","assignedAgent":null}'),
 (2,2,1,N'DetailsChanged',N'subject',N'"History original subject"',N'"New <img>"'),
 (3,2,2,N'DetailsChanged',N'description',N'"وصف قديم"',N'"تفاصيل جديدة\n محفوظة"'),
 (4,2,3,N'DetailsChanged',N'category',N'{"id":"40000000-0000-0000-0000-000000000001","displayName":"History category one"}',N'{"id":"40000000-0000-0000-0000-000000000002","displayName":"History category two"}'),
 (5,2,4,N'DetailsChanged',N'priority',N'"Normal"',N'"High"'),
 (6,9,1,N'AssignmentChanged',N'assignedAgent',NULL,N'{"id":"50000000-0000-0000-0000-000000000001","displayName":"History agent one"}'),
 (7,10,1,N'AssignmentChanged',N'assignedAgent',N'{"id":"50000000-0000-0000-0000-000000000001","displayName":"History agent one"}',N'{"id":"50000000-0000-0000-0000-000000000002","displayName":"History agent two"}'),
 (8,11,1,N'AssignmentChanged',N'assignedAgent',N'{"id":"50000000-0000-0000-0000-000000000002","displayName":"History agent two"}',NULL),
 (9,12,1,N'StatusChanged',N'status',N'"Open"',N'"InProgress"'),
 (10,13,1,N'StatusChanged',N'status',N'"InProgress"',N'"Resolved"'),
 (11,14,1,N'StatusChanged',N'status',N'"Resolved"',N'"Closed"'),
 (12,15,1,N'StatusChanged',N'status',N'"Closed"',N'"Open"'),
 (13,16,1,N'AssignmentChanged',N'assignedAgent',NULL,N'{"id":"50000000-0000-0000-0000-000000000001","displayName":"History agent one"}');
DECLARE @index int=14;
WHILE @index<=31
BEGIN
 DECLARE @revision bigint=@index+86;
 INSERT @entries VALUES(@index,@revision,1,N'DetailsChanged',N'subject',
  CASE WHEN @index=14 THEN N'"New <img>"' ELSE N'"History update '+CONVERT(nvarchar(20),@revision-1)+N'"' END,
  N'"History update '+CONVERT(nvarchar(20),@revision)+N'"');
 SET @index+=1;
END;
INSERT @entries VALUES
 (32,9007199254740992,1,N'DetailsChanged',N'subject',N'"History update 117"',N'"Before final history"'),
 (33,9007199254740993,1,N'DetailsChanged',N'subject',N'"Before final history"',N'"Current history ticket"');
-- Insert in reverse order; reads must use numeric revision/sequence, never insertion/ID/time order.
INSERT dbo.TicketHistoryEntries(Id,TicketId,Revision,Sequence,Kind,Field,PreviousValue,NewValue,ActorIssuer,ActorSubject,ActorDisplayName,OccurredAtUtc)
SELECT TOP (33) CONVERT(uniqueidentifier,N'70000000-0000-0000-0000-'+RIGHT(REPLICATE('0',12)+CONVERT(nvarchar(12),Ordinal),12)),
 @ticket,Revision,Sequence,Kind,Field,PreviousValue,NewValue,N'https://example.test',
 CASE WHEN Ordinal=1 THEN N'history-creator' WHEN Ordinal<=5 OR Ordinal%2=0 THEN N'history-staff-a' ELSE N'history-staff-b' END,
 CASE WHEN Ordinal=1 THEN N'Historical Creator' WHEN Ordinal<=5 OR Ordinal%2=0 THEN N'Historical Staff A' ELSE N'Historical Staff B' END,
 '2026-10-08T09:00:00+00:00' FROM @entries ORDER BY Ordinal DESC;
COMMIT;
