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
                THROW 51001, 'Departman yöneticisi geçişi eski elle atanmış Employee.ManagerId verilerine kayıpsız döndürülemez.', 1;
                """);
        }
    }
}
