class UserInfo {
  final String userId;
  final String email;
  final String? displayName;
  final String? profileImageUrl;

  const UserInfo({
    required this.userId,
    required this.email,
    this.displayName,
    this.profileImageUrl,
  });

  factory UserInfo.fromJson(Map<String, dynamic> json) => UserInfo(
        userId: json['userId'] as String,
        email: json['email'] as String,
        displayName: json['displayName'] as String?,
        profileImageUrl: json['profileImageUrl'] as String?,
      );
}

class Project {
  final String projectId;
  final String projectName;
  final String? description;
  final String? status;
  final bool askUserEnabled;
  final int maxDiscussionRounds;
  final int maxDiscussionParticipants;
  final DateTime? createdAt;

  const Project({
    required this.projectId,
    required this.projectName,
    this.description,
    this.status,
    this.askUserEnabled = true,
    this.maxDiscussionRounds = 10,
    this.maxDiscussionParticipants = 4,
    this.createdAt,
  });

  factory Project.fromJson(Map<String, dynamic> json) => Project(
        projectId: json['projectId'] as String,
        projectName: json['projectName'] as String,
        description: json['description'] as String?,
        status: json['status'] as String?,
        askUserEnabled: json['askUserEnabled'] as bool? ?? true,
        maxDiscussionRounds: json['maxDiscussionRounds'] as int? ?? 10,
        maxDiscussionParticipants: json['maxDiscussionParticipants'] as int? ?? 4,
        createdAt: json['createdAt'] != null
            ? DateTime.tryParse(json['createdAt'] as String)
            : null,
      );
}

class ChatMessage {
  final String eventId;
  final String eventType;
  final Map<String, dynamic>? payload;
  final String? modelUsed;
  final DateTime createdAt;

  const ChatMessage({
    required this.eventId,
    required this.eventType,
    this.payload,
    this.modelUsed,
    required this.createdAt,
  });

  factory ChatMessage.fromJson(Map<String, dynamic> json) => ChatMessage(
        eventId: json['eventId'] as String,
        eventType: json['eventType'] as String,
        payload: json['payload'] as Map<String, dynamic>?,
        modelUsed: json['modelUsed'] as String?,
        createdAt: DateTime.parse(json['createdAt'] as String),
      );

  bool get isUser => eventType == 'USER_MESSAGE';

  String get displayContent {
    if (payload == null) return '';
    return payload!['message'] as String? ??
        payload!['content'] as String? ??
        payload!['text'] as String? ??
        '';
  }

  String get senderLabel {
    if (isUser) return '나';
    if (payload == null) return eventType;
    return payload!['personaLabel'] as String? ??
        payload!['label'] as String? ??
        eventType;
  }
}

class AgentTurn {
  final String? personaId;
  final String? personaLabel;
  final String? personaAvatar;
  final String content;
  final String? modelUsed;
  final bool isPm;
  final int turnIndex;

  const AgentTurn({
    this.personaId,
    this.personaLabel,
    this.personaAvatar,
    required this.content,
    this.modelUsed,
    required this.isPm,
    required this.turnIndex,
  });

  factory AgentTurn.fromJson(Map<String, dynamic> json) => AgentTurn(
        personaId: json['personaId'] as String?,
        personaLabel: json['personaLabel'] as String?,
        personaAvatar: json['personaAvatar'] as String?,
        content: json['content'] as String? ?? '',
        modelUsed: json['modelUsed'] as String?,
        isPm: json['isPm'] as bool? ?? false,
        turnIndex: json['turnIndex'] as int? ?? 0,
      );
}

class ChatResponse {
  final String eventId;
  final String? personaLabel;
  final String content;
  final List<AgentTurn> turns;

  const ChatResponse({
    required this.eventId,
    this.personaLabel,
    required this.content,
    required this.turns,
  });

  factory ChatResponse.fromJson(Map<String, dynamic> json) => ChatResponse(
        eventId: json['eventId'] as String,
        personaLabel: json['personaLabel'] as String?,
        content: json['content'] as String? ?? '',
        turns: (json['turns'] as List<dynamic>? ?? [])
            .map((t) => AgentTurn.fromJson(t as Map<String, dynamic>))
            .toList(),
      );
}

class Persona {
  final String personaId;
  final String name;
  final String? label;
  final String? description;
  final String? avatar;
  final String? icon;
  final String? color;
  final bool isPm;
  final String? primaryModel;
  final String? fallbackModel;
  final double temperature;
  final int maxTokens;

  const Persona({
    required this.personaId,
    required this.name,
    this.label,
    this.description,
    this.avatar,
    this.icon,
    this.color,
    required this.isPm,
    this.primaryModel,
    this.fallbackModel,
    this.temperature = 0.7,
    this.maxTokens = 4096,
  });

  factory Persona.fromJson(Map<String, dynamic> json) => Persona(
        personaId: json['personaId'] as String,
        name: json['name'] as String,
        label: json['label'] as String?,
        description: json['description'] as String?,
        avatar: json['avatar'] as String?,
        icon: json['icon'] as String?,
        color: json['color'] as String?,
        isPm: json['isPm'] as bool? ?? false,
        primaryModel: json['primaryModel'] as String?,
        fallbackModel: json['fallbackModel'] as String?,
        temperature: (json['temperature'] as num?)?.toDouble() ?? 0.7,
        maxTokens: json['maxTokens'] as int? ?? 4096,
      );
}

class WikiDocument {
  final String wikiId;
  final String? category;
  final String title;
  final String? content;
  final int version;
  final DateTime? updatedAt;

  const WikiDocument({
    required this.wikiId,
    this.category,
    required this.title,
    this.content,
    required this.version,
    this.updatedAt,
  });

  factory WikiDocument.fromJson(Map<String, dynamic> json) => WikiDocument(
        wikiId: json['wikiId'] as String,
        category: json['category'] as String?,
        title: json['title'] as String,
        content: json['content'] as String?,
        version: json['version'] as int? ?? 1,
        updatedAt: json['updatedAt'] != null
            ? DateTime.tryParse(json['updatedAt'] as String)
            : null,
      );
}

class TimelineEvent {
  final String eventId;
  final String eventType;
  final String? modelUsed;
  final String? triggeredBy;
  final DateTime createdAt;

  const TimelineEvent({
    required this.eventId,
    required this.eventType,
    this.modelUsed,
    this.triggeredBy,
    required this.createdAt,
  });

  factory TimelineEvent.fromJson(Map<String, dynamic> json) => TimelineEvent(
        eventId: json['eventId'] as String,
        eventType: json['eventType'] as String,
        modelUsed: json['modelUsed'] as String?,
        triggeredBy: json['triggeredBy'] as String?,
        createdAt: DateTime.parse(json['createdAt'] as String),
      );
}
