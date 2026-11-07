import '../../core/network/api_response_helper.dart';

class LibraryItemModel {
  final int id;
  final String title;
  final String description;
  final String category;
  final String? pdfFilePath;
  final String uploadedBy;
  final int downloadCount;
  final String? createdAt;

  const LibraryItemModel({
    required this.id,
    required this.title,
    required this.description,
    required this.category,
    this.pdfFilePath,
    required this.uploadedBy,
    required this.downloadCount,
    this.createdAt,
  });

  factory LibraryItemModel.fromJson(Map<String, dynamic> json) =>
      LibraryItemModel(
        id: toInt(json['id']),
        title: toStr(json['title']),
        description: toStr(json['description']),
        category: toStr(json['category']),
        pdfFilePath: json['pdfFilePath']?.toString(),
        uploadedBy: toStr(json['uploadedBy'], 'غير معروف'),
        downloadCount: toInt(json['downloadCount']),
        createdAt: json['createdAt']?.toString(),
      );
}
