using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IK.Web.Migrations
{
    /// <inheritdoc />
    public partial class BackfillDepartmentManagerAuthority : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF EXISTS (
                    SELECT employee.DepartmentId
                    FROM Employees AS employee
                    INNER JOIN Departments AS department
                        ON department.DepartmentId = employee.DepartmentId
                    INNER JOIN Employees AS candidate
                        ON candidate.EmployeeId = employee.ManagerId
                       AND candidate.DepartmentId = employee.DepartmentId
                       AND candidate.Status = 1
                    WHERE employee.ManagerId IS NOT NULL
                      AND department.ManagerEmployeeId IS NULL
                    GROUP BY employee.DepartmentId
                    HAVING COUNT(DISTINCT employee.ManagerId) > 1
                )
                BEGIN
                    THROW 51000, 'Aynı departman için birden fazla eski yönetici bulundu. Departman yöneticisi geçişi güvenli biçimde tamamlanamadı.', 1;
                END;

                IF EXISTS (
                    SELECT 1
                    FROM Employees AS employee
                    LEFT JOIN Employees AS candidate
                        ON candidate.EmployeeId = employee.ManagerId
                    WHERE employee.ManagerId IS NOT NULL
                      AND (
                          candidate.EmployeeId IS NULL
                          OR candidate.DepartmentId <> employee.DepartmentId
                          OR candidate.Status <> 1
                      )
                )
                BEGIN
                    THROW 51001, 'Eski yönetici bağlantılarından en az biri aktif ve aynı departmanda değil; güvenli geçiş durduruldu.', 1;
                END;

                ;WITH InferredManagers AS (
                    SELECT
                        employee.DepartmentId,
                        MIN(employee.ManagerId) AS ManagerEmployeeId
                    FROM Employees AS employee
                    INNER JOIN Departments AS department
                        ON department.DepartmentId = employee.DepartmentId
                    INNER JOIN Employees AS candidate
                        ON candidate.EmployeeId = employee.ManagerId
                       AND candidate.DepartmentId = employee.DepartmentId
                       AND candidate.Status = 1
                    WHERE employee.ManagerId IS NOT NULL
                      AND department.ManagerEmployeeId IS NULL
                    GROUP BY employee.DepartmentId
                    HAVING COUNT(DISTINCT employee.ManagerId) = 1
                )
                UPDATE department
                SET department.ManagerEmployeeId = inferred.ManagerEmployeeId
                FROM Departments AS department
                INNER JOIN InferredManagers AS inferred
                    ON inferred.DepartmentId = department.DepartmentId;

                IF EXISTS (
                    SELECT 1
                    FROM Departments AS department
                    LEFT JOIN Employees AS managerEmployee
                        ON managerEmployee.EmployeeId = department.ManagerEmployeeId
                    WHERE department.ManagerEmployeeId IS NOT NULL
                      AND (
                          managerEmployee.EmployeeId IS NULL
                          OR managerEmployee.DepartmentId <> department.DepartmentId
                          OR managerEmployee.Status <> 1
                      )
                )
                BEGIN
                    THROW 51002, 'Departman yöneticilerinden en az biri aktif ve kendi departmanında değil; güvenli geçiş durduruldu.', 1;
                END;

                DECLARE @DepartmentCycleFound bit = 0;
                ;WITH DepartmentHierarchy AS (
                    SELECT
                        department.DepartmentId AS OriginDepartmentId,
                        department.ParentDepartmentId,
                        CAST('/' + CAST(department.DepartmentId AS varchar(11)) + '/' AS varchar(max)) AS VisitedPath,
                        CAST(0 AS bit) AS HasCycle
                    FROM Departments AS department

                    UNION ALL

                    SELECT
                        hierarchy.OriginDepartmentId,
                        parent.ParentDepartmentId,
                        CAST(
                            hierarchy.VisitedPath
                            + CAST(parent.DepartmentId AS varchar(11))
                            + '/'
                            AS varchar(max)
                        ),
                        CAST(
                            CASE
                                WHEN hierarchy.VisitedPath LIKE '%/' + CAST(parent.DepartmentId AS varchar(11)) + '/%'
                                    THEN 1
                                ELSE 0
                            END
                            AS bit
                        )
                    FROM DepartmentHierarchy AS hierarchy
                    INNER JOIN Departments AS parent
                        ON parent.DepartmentId = hierarchy.ParentDepartmentId
                    WHERE hierarchy.HasCycle = 0
                )
                SELECT TOP (1) @DepartmentCycleFound = 1
                FROM DepartmentHierarchy
                WHERE HasCycle = 1
                OPTION (MAXRECURSION 32767);

                IF @DepartmentCycleFound = 1
                BEGIN
                    THROW 51003, 'Departman hiyerarşisinde döngü bulundu; güvenli geçiş durduruldu.', 1;
                END;

                IF EXISTS (
                    SELECT 1
                    FROM Departments AS childDepartment
                    INNER JOIN Departments AS parentDepartment
                        ON parentDepartment.DepartmentId = childDepartment.ParentDepartmentId
                    WHERE childDepartment.ManagerEmployeeId IS NOT NULL
                      AND parentDepartment.ManagerEmployeeId IS NULL
                )
                BEGIN
                    THROW 51004, 'Yöneticisi olan bir alt departmanın üst departman yöneticisi eksik; güvenli geçiş durduruldu.', 1;
                END;

                UPDATE Employees
                SET ManagerId = NULL;

                UPDATE employee
                SET employee.ManagerId = department.ManagerEmployeeId
                FROM Employees AS employee
                INNER JOIN Departments AS department
                    ON department.DepartmentId = employee.DepartmentId
                WHERE employee.EmployeeId <> department.ManagerEmployeeId;

                UPDATE managerEmployee
                SET managerEmployee.ManagerId = parentDepartment.ManagerEmployeeId
                FROM Departments AS department
                INNER JOIN Employees AS managerEmployee
                    ON managerEmployee.EmployeeId = department.ManagerEmployeeId
                LEFT JOIN Departments AS parentDepartment
                    ON parentDepartment.DepartmentId = department.ParentDepartmentId;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                THROW 51005, 'Departman yöneticisi geçişi eski elle atanmış Employee.ManagerId verilerine kayıpsız döndürülemez.', 1;
                """);
        }
    }
}
