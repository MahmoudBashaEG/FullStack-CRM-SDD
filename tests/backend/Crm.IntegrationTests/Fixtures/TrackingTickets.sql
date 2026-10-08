-- Synthetic prepared records only; never a supported mutation/admin path.
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;
IF NOT EXISTS(SELECT 1 FROM dbo.CategoryReferences WHERE Id='30000000-0000-0000-0000-000000000001')
 INSERT dbo.CategoryReferences VALUES('30000000-0000-0000-0000-000000000001',N'Test Category',1);
IF NOT EXISTS(SELECT 1 FROM dbo.CategoryReferences WHERE Id='30000000-0000-0000-0000-000000000002')
 INSERT dbo.CategoryReferences VALUES('30000000-0000-0000-0000-000000000002',N'Inactive Category',0);
IF NOT EXISTS(SELECT 1 FROM dbo.AgentReferences WHERE Id='20000000-0000-0000-0000-000000000001')
 INSERT dbo.AgentReferences(Id,Issuer,Subject,DisplayName,IsEligible) VALUES('20000000-0000-0000-0000-000000000001',N'https://example.test',N'agent-a',N'Test Agent',1);
IF NOT EXISTS(SELECT 1 FROM dbo.AgentReferences WHERE Id='20000000-0000-0000-0000-000000000002')
 INSERT dbo.AgentReferences(Id,Issuer,Subject,DisplayName,IsEligible) VALUES('20000000-0000-0000-0000-000000000002',N'https://example.test',N'agent-b',N'Inactive Agent',0);
DECLARE @rows TABLE(Ordinal int, Subject nvarchar(max), Status nvarchar(16), Priority nvarchar(16), Category int, Agent int, Updated datetimeoffset);
INSERT @rows VALUES
 (1,N'TrackingSeed Alpha Service',N'Open',N'Normal',1,1,'2026-10-08T12:00:00+00:00'),
 (2,N'TrackingSeed alpha hardware',N'InProgress',N'High',1,2,'2026-10-08T12:00:00+00:00'),
 (3,N'TrackingSeed مشكلة اتصال',N'Resolved',N'High',2,2,'2026-10-08T11:00:00+00:00'),
 (4,N'TrackingSeed literal %_[brackets]\ mark',N'Closed',N'Low',0,0,'2026-10-08T11:00:00+00:00'),
 (5,N'TrackingSeed ALPHA café',N'Closed',N'Normal',1,0,'2026-10-08T10:00:00+00:00'),
 (6,N'TrackingSeed Beta',N'Open',N'Urgent',2,1,'2026-10-08T12:00:00+00:00'),
 (7,N'TrackingSeed Gamma',N'Resolved',N'Low',0,2,'2026-10-08T10:00:00+00:00'),
 (8,N'TrackingSeed reference TKT-10000000000000000000000000000001',N'InProgress',N'High',0,0,'2026-10-08T10:00:00+00:00');
DECLARE @i int=9;
WHILE @i<=32
BEGIN
 INSERT @rows VALUES(@i,N'TrackingSeed Paging '+CONVERT(nvarchar(10),@i),CASE @i%4 WHEN 0 THEN N'Open' WHEN 1 THEN N'InProgress' WHEN 2 THEN N'Resolved' ELSE N'Closed' END,
 CASE @i%4 WHEN 0 THEN N'Low' WHEN 1 THEN N'Normal' WHEN 2 THEN N'High' ELSE N'Urgent' END,@i%3,@i%3,'2026-10-08T09:00:00+00:00');
 SET @i+=1;
END;
INSERT dbo.Tickets(Id,Reference,Subject,Description,CategoryId,Priority,Status,AssignedAgentId,CreatedAtUtc,UpdatedAtUtc,Revision)
SELECT CONVERT(uniqueidentifier,N'10000000-0000-0000-0000-'+RIGHT(REPLICATE('0',12)+CONVERT(varchar(12),Ordinal),12)),
 N'TKT-10000000000000000000'+RIGHT(REPLICATE('0',12)+CONVERT(varchar(12),Ordinal),12),Subject,N'Synthetic prepared tracking ticket',
 CASE Category WHEN 0 THEN '00000000-0000-0000-0000-000000000001' WHEN 1 THEN '30000000-0000-0000-0000-000000000001' ELSE '30000000-0000-0000-0000-000000000002' END,
 Priority,Status,CASE Agent WHEN 0 THEN NULL WHEN 1 THEN '20000000-0000-0000-0000-000000000001' ELSE '20000000-0000-0000-0000-000000000002' END,
 '2026-10-08T08:00:00+00:00',Updated,1 FROM @rows r
WHERE NOT EXISTS(SELECT 1 FROM dbo.Tickets WHERE Id=CONVERT(uniqueidentifier,N'10000000-0000-0000-0000-'+RIGHT(REPLICATE('0',12)+CONVERT(varchar(12),r.Ordinal),12)));
INSERT dbo.TicketHistoryEntries(Id,TicketId,Revision,Sequence,Kind,Field,PreviousValue,NewValue,ActorIssuer,ActorSubject,ActorDisplayName,OccurredAtUtc)
SELECT NEWID(),t.Id,1,1,N'Created',NULL,NULL,
 (SELECT t.Reference AS reference,t.Subject AS subject,t.Description AS description,
 JSON_QUERY((SELECT c.Id AS id,c.DisplayName AS displayName FOR JSON PATH,WITHOUT_ARRAY_WRAPPER)) AS category,
 t.Priority AS priority,N'Open' AS status,NULL AS assignedAgent FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER),
 N'https://example.test',N'fixture-staff',N'Fixture Staff',t.CreatedAtUtc
FROM dbo.Tickets t JOIN dbo.CategoryReferences c ON c.Id=t.CategoryId
WHERE t.Subject LIKE N'TrackingSeed %' AND NOT EXISTS(SELECT 1 FROM dbo.TicketHistoryEntries h WHERE h.TicketId=t.Id);
COMMIT;
