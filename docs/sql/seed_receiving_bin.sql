-- Her depoda sabit bir mal kabul hücresi (KABUL) garanti edilir.
-- WMS standardı: mallar kabulde bu hücreye iner, sonra putaway ile raf hücresine taşınır.
-- Idempotent: yalnızca IsReceivingArea hücresi olmayan depolara eklenir.
INSERT INTO Bin (Id, WarehouseId, Code, Zone, SortNo, IsPickingArea, IsReceivingArea, IsActive, IsDeleted)
SELECT NEWID(), w.Id, N'KABUL', N'KABUL', 0, 0, 1, 1, 0
FROM Warehouse w
WHERE w.IsDeleted = 0
  AND NOT EXISTS (
      SELECT 1 FROM Bin b
      WHERE b.WarehouseId = w.Id AND b.IsReceivingArea = 1 AND b.IsDeleted = 0
  );

SELECT w.Code AS Depo,
       SUM(CASE WHEN b.IsReceivingArea = 1 THEN 1 ELSE 0 END) AS KabulBin
FROM Warehouse w
LEFT JOIN Bin b ON b.WarehouseId = w.Id AND b.IsDeleted = 0
WHERE w.IsDeleted = 0
GROUP BY w.Code;
